using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Files;
using RepairShop.Domain.Common;
using RepairShop.Domain.Files;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.RepairOrders;

/// <summary>Notes, attachments/photos, checklists and history of an order.</summary>
public sealed class OrderRecordsService
{
    private const string EntityType = RepairOrderService.EntityType;

    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderNoteRepository _notes;
    private readonly IRepairOrderAttachmentRepository _attachments;
    private readonly IRepairOrderReceptionChecklistRepository _reception;
    private readonly IRepairOrderQaChecklistRepository _qa;
    private readonly IRepairOrderStatusHistoryRepository _history;
    private readonly IStoredFileRepository _storedFiles;
    private readonly IUserRepository _users;
    private readonly FileService _files;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IFileUrlSigner _urls;

    public OrderRecordsService(
        IRepairOrderRepository orders,
        IRepairOrderNoteRepository notes,
        IRepairOrderAttachmentRepository attachments,
        IRepairOrderReceptionChecklistRepository reception,
        IRepairOrderQaChecklistRepository qa,
        IRepairOrderStatusHistoryRepository history,
        IStoredFileRepository storedFiles,
        IUserRepository users,
        FileService files,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IFileUrlSigner urls)
    {
        _urls = urls;
        _orders = orders;
        _notes = notes;
        _attachments = attachments;
        _reception = reception;
        _qa = qa;
        _history = history;
        _storedFiles = storedFiles;
        _users = users;
        _files = files;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    // ===== Notes =====

    public async Task<List<RepairOrderNoteResponse>> ListNotesAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var notes = await _notes.ListByOrderAsync(shopId, orderId, ct);
        var names = await NamesAsync(notes.Select(n => n.CreatedByUserId), ct);
        return notes.Select(n => new RepairOrderNoteResponse(n.Id, n.Body, n.CreatedByUserId, n.CreatedAtUtc, n.IsPublic, names.GetValueOrDefault(n.CreatedByUserId))).ToList();
    }

    public async Task<RepairOrderNoteResponse> AddNoteAsync(Guid shopId, Guid orderId, CreateRepairOrderNoteRequest req, Actor actor, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var note = new RepairOrderNote(shopId, orderId, req.Body, req.IsPublic, actor.UserId, _clock.UtcNow);
        await _notes.AddAsync(note, ct);
        if (req.IsPublic) await _audit.AddAsync(shopId, EntityType, orderId, "public_note_added", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        var names = await NamesAsync(new[] { actor.UserId }, ct);
        return new RepairOrderNoteResponse(note.Id, note.Body, note.CreatedByUserId, note.CreatedAtUtc, note.IsPublic, names.GetValueOrDefault(actor.UserId));
    }

    // ===== Attachments =====

    public async Task<List<RepairOrderAttachmentResponse>> ListAttachmentsAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var list = await _attachments.ListByOrderAsync(shopId, orderId, ct);
        var result = new List<RepairOrderAttachmentResponse>();
        foreach (var a in list) result.Add(await ToResponseAsync(shopId, a, ct));
        return result;
    }

    public async Task<RepairOrderAttachmentResponse> AddLinkAsync(Guid shopId, Guid orderId, CreateRepairOrderAttachmentRequest req, Actor actor, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var att = new RepairOrderAttachment(shopId, orderId, req.Url, req.Label, actor.UserId, _clock.UtcNow);
        await _attachments.AddAsync(att, ct);
        await _uow.SaveChangesAsync(ct);
        return await ToResponseAsync(shopId, att, ct);
    }

    public async Task<RepairOrderAttachmentResponse> UploadAsync(Guid shopId, Guid orderId, Stream content, string? fileName, string? label, Actor actor, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var file = await _files.UploadAsync(shopId, content, fileName, "order_photo", imagesOnly: false, actor.UserId, ct);
        var kind = file.IsImage ? AttachmentKind.Photo : AttachmentKind.Document;
        var att = RepairOrderAttachment.ForFile(shopId, orderId, kind, file.Id, label ?? file.FileName, actor.UserId, _clock.UtcNow);
        await _attachments.AddAsync(att, ct);
        await _audit.AddAsync(shopId, EntityType, orderId, "attachment_uploaded", actor, new { fileId = file.Id, kind = kind.ToString(), file.SizeBytes }, ct);
        await _uow.SaveChangesAsync(ct);
        return await ToResponseAsync(shopId, att, ct);
    }

    public async Task DeleteAttachmentAsync(Guid shopId, Guid orderId, Guid attachmentId, Actor actor, CancellationToken ct)
    {
        var att = await _attachments.GetByIdAsync(shopId, attachmentId, ct);
        if (att is null || att.RepairOrderId != orderId) throw new NotFoundException("Adjunto no encontrado.");
        _attachments.Remove(att);
        await _audit.AddAsync(shopId, EntityType, orderId, "attachment_deleted", actor, new { attachmentId, att.FileId }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // ===== Reception checklist =====

    public async Task<RepairOrderChecklistResponse?> GetReceptionAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var cl = await _reception.GetByOrderAsync(shopId, orderId, ct);
        return cl is null ? null : ToResponse(cl);
    }

    public async Task<RepairOrderChecklistResponse> UpsertReceptionAsync(Guid shopId, Guid orderId, UpdateRepairOrderChecklistRequest b, Actor actor, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var now = _clock.UtcNow;
        var cl = await _reception.GetByOrderAsync(shopId, orderId, ct);
        if (cl is null)
        {
            cl = new RepairOrderReceptionChecklist(shopId, orderId, actor.UserId, now);
            await _reception.AddAsync(cl, ct);
        }

        cl.Update(b.ScreenOk, b.CamerasOk, b.SpeakersOk, b.MicrophoneOk, b.ButtonsOk, b.FaceIdOk, b.FingerprintOk, b.CloudLock, b.BatteryPercent, b.CosmeticNotes, actor.UserId, now);
        await _audit.AddAsync(shopId, EntityType, orderId, "checklist_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(cl);
    }

    // ===== QA (exit) checklist =====

    public async Task<QaChecklistResponse?> GetQaAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var qa = await _qa.GetByOrderAsync(shopId, orderId, ct);
        return qa is null ? null : ToResponse(qa);
    }

    public async Task<QaChecklistResponse> UpsertQaAsync(Guid shopId, Guid orderId, UpdateQaChecklistRequest b, Actor actor, CancellationToken ct)
    {
        var order = await RequireOrderAsync(shopId, orderId, ct);
        if (order.IsFinal) throw new DomainException("La orden está finalizada.");
        if (order.Status is RepairOrderStatus.Received or RepairOrderStatus.Diagnosing)
            throw new DomainException("El control de calidad se completa una vez que la reparación está en curso.");

        var now = _clock.UtcNow;
        var qa = await _qa.GetByOrderAsync(shopId, orderId, ct);
        if (qa is null)
        {
            qa = new RepairOrderQaChecklist(shopId, orderId, actor.UserId, now);
            await _qa.AddAsync(qa, ct);
        }

        qa.Update(b.PowersOn, b.ScreenOk, b.TouchOk, b.CamerasOk, b.AudioOk, b.MicrophoneOk, b.ButtonsOk, b.ChargingOk, b.ConnectivityOk, b.BiometricsOk,
            b.BatteryHealthPercent, b.Notes, b.Approve, actor.UserId, now);
        await _audit.AddAsync(shopId, EntityType, orderId, b.Approve ? "qa_passed" : "qa_saved", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(qa);
    }

    // ===== History =====

    public async Task<List<OrderStatusHistoryResponse>> ListHistoryAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var list = await _history.ListByOrderAsync(shopId, orderId, ct);
        var names = await NamesAsync(list.Select(h => h.ChangedByUserId), ct);
        return list.Select(h => new OrderStatusHistoryResponse(h.Id, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedAtUtc,
            OrderLabels.Status(h.FromStatus), OrderLabels.Status(h.ToStatus), names.GetValueOrDefault(h.ChangedByUserId), h.Reason)).ToList();
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<RepairOrder> RequireOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Where(x => x != Guid.Empty).Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, string>();
        return (await _users.GetByIdsAsync(list, ct)).ToDictionary(u => u.Id, u => u.DisplayName);
    }

    private async Task<RepairOrderAttachmentResponse> ToResponseAsync(Guid shopId, RepairOrderAttachment a, CancellationToken ct)
    {
        StoredFile? file = a.FileId is null ? null : await _storedFiles.GetByIdAsync(shopId, a.FileId.Value, ct);
        // Files get a short-lived signed URL so they can be shown in <img> tags without the bearer token.
        var url = a.FileId is null ? a.Url : _urls.GetUrl(a.FileId.Value, TimeSpan.FromMinutes(30));
        return new RepairOrderAttachmentResponse(a.Id, url, a.Label, a.CreatedByUserId, a.CreatedAtUtc, a.Kind.ToString(), a.FileId,
            file?.FileName, file?.ContentType, file?.SizeBytes);
    }

    private static RepairOrderChecklistResponse ToResponse(RepairOrderReceptionChecklist c)
        => new(c.Id, c.RepairOrderId, c.ScreenOk, c.CamerasOk, c.SpeakersOk, c.MicrophoneOk, c.ButtonsOk, c.FaceIdOk, c.FingerprintOk,
            c.CloudLock, c.BatteryPercent, c.CosmeticNotes, c.UpdatedByUserId, c.UpdatedAtUtc);

    private static QaChecklistResponse ToResponse(RepairOrderQaChecklist q)
        => new(q.Id, q.RepairOrderId, q.PowersOn, q.ScreenOk, q.TouchOk, q.CamerasOk, q.AudioOk, q.MicrophoneOk, q.ButtonsOk, q.ChargingOk,
            q.ConnectivityOk, q.BiometricsOk, q.BatteryHealthPercent, q.Notes, q.Passed, q.CheckedByUserId, q.CheckedAtUtc);
}
