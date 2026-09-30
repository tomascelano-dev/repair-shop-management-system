using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class OrdersController : ControllerBase
{
    private const string EntityTypeRepairOrder = "repair_order";

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RepairOrderResponse>>>> List(
        [FromServices] IRepairOrderRepository repo,
        [FromQuery] string? q = null,
        [FromQuery] RepairOrderStatus? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var (items, total) = await repo.SearchAsync(shopId, new RepairOrderSearchOptions(
            Q: q,
            Status: status,
            DateFromUtc: dateFrom?.ToUniversalTime(),
            DateToUtc: dateTo?.ToUniversalTime(),
            SortBy: sortBy,
            SortDir: sortDir,
            Skip: skip,
            Take: take
        ), ct);

        Response.Headers["X-Total-Count"] = total.ToString();

        var res = items.Select(ToResponse).ToList();
        return Ok(new ApiResponse<List<RepairOrderResponse>>(res));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> GetById(
        [FromServices] IRepairOrderRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var o = await repo.GetByIdAsync(shopId, id, ct);
        if (o is null) return NotFound();

        return Ok(new ApiResponse<RepairOrderResponse>(ToResponse(o)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Create(
        [FromServices] IRepairOrderRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        [FromBody] RepairOrderCreateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var order = new RepairOrder(shopId, body.CustomerId, body.DeviceId, body.IssueDescription, body.Notes, clock.UtcNow);
        await repo.AddAsync(order, ct);
        await uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new ApiResponse<RepairOrderResponse>(ToResponse(order)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Update(
        [FromServices] IRepairOrderRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] RepairOrderUpdateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var order = await repo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        order.Update(body.IssueDescription, body.Notes, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return Ok(new ApiResponse<RepairOrderResponse>(ToResponse(order)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(
        [FromServices] IRepairOrderRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var order = await repo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        await repo.RemoveAsync(order, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    // PRO: status transition + history + audit + outbox + suggested message
    [HttpPost("{id:guid}/status")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<ChangeOrderStatusResponse>>> ChangeStatus(
        [FromServices] ChangeOrderStatusService service,
        Guid id,
        [FromBody] ChangeOrderStatusRequest body,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        var shopId = CurrentUser.GetShopId(User);
        var email = CurrentUser.GetEmail(User);
        if (userId == Guid.Empty || shopId == Guid.Empty) return Unauthorized();

        var res = await service.HandleAsync(
            shopId: shopId,
            orderId: id,
            newStatus: body.Status,
            actorUserId: userId,
            actorEmail: email,
            enqueueOutbox: body.EnqueueOutbox,
            channel: body.Channel,
            ct: ct);

        return Ok(new ApiResponse<ChangeOrderStatusResponse>(res));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<List<OrderStatusHistoryResponse>>>> History(
        [FromServices] IRepairOrderStatusHistoryRepository historyRepo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await historyRepo.ListByOrderAsync(shopId, id, ct);
        var res = list.Select(h => new OrderStatusHistoryResponse(h.Id, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedAtUtc)).ToList();
        return Ok(new ApiResponse<List<OrderStatusHistoryResponse>>(res));
    }

    // Notes
    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderNoteResponse>>>> Notes(
        [FromServices] IRepairOrderNoteRepository notesRepo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await notesRepo.ListByOrderAsync(shopId, id, ct);
        var res = list.Select(n => new RepairOrderNoteResponse(n.Id, n.Body, n.CreatedByUserId, n.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<RepairOrderNoteResponse>>(res));
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<ApiResponse<RepairOrderNoteResponse>>> AddNote(
        [FromServices] IRepairOrderRepository ordersRepo,
        [FromServices] IRepairOrderNoteRepository notesRepo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] CreateRepairOrderNoteRequest body,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        var shopId = CurrentUser.GetShopId(User);
        if (userId == Guid.Empty || shopId == Guid.Empty) return Unauthorized();

        var order = await ordersRepo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        var note = new RepairOrderNote(shopId, id, body.Body, userId, clock.UtcNow);
        await notesRepo.AddAsync(note, ct);
        await uow.SaveChangesAsync(ct);

        var res = new RepairOrderNoteResponse(note.Id, note.Body, note.CreatedByUserId, note.CreatedAtUtc);
        return Ok(new ApiResponse<RepairOrderNoteResponse>(res));
    }

    // Attachments
    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderAttachmentResponse>>>> Attachments(
        [FromServices] IRepairOrderAttachmentRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await repo.ListByOrderAsync(shopId, id, ct);
        var res = list.Select(a => new RepairOrderAttachmentResponse(a.Id, a.Url, a.Label, a.CreatedByUserId, a.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<RepairOrderAttachmentResponse>>(res));
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<ApiResponse<RepairOrderAttachmentResponse>>> AddAttachment(
        [FromServices] IRepairOrderRepository ordersRepo,
        [FromServices] IRepairOrderAttachmentRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] CreateRepairOrderAttachmentRequest body,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        var shopId = CurrentUser.GetShopId(User);
        if (userId == Guid.Empty || shopId == Guid.Empty) return Unauthorized();

        var order = await ordersRepo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        var att = new RepairOrderAttachment(shopId, id, body.Url, body.Label, userId, clock.UtcNow);
        await repo.AddAsync(att, ct);
        await uow.SaveChangesAsync(ct);

        var res = new RepairOrderAttachmentResponse(att.Id, att.Url, att.Label, att.CreatedByUserId, att.CreatedAtUtc);
        return Ok(new ApiResponse<RepairOrderAttachmentResponse>(res));
    }

    // PRO: quote
    [HttpPut("{id:guid}/quote")]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> SetQuote(
        [FromServices] IRepairOrderRepository ordersRepo,
        [FromServices] IAuditEventRepository auditRepo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] SetOrderQuoteRequest body,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        var shopId = CurrentUser.GetShopId(User);
        var email = CurrentUser.GetEmail(User);
        if (userId == Guid.Empty || shopId == Guid.Empty) return Unauthorized();

        var order = await ordersRepo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        if (body.Amount is null)
        {
            order.ClearQuote(userId, clock.UtcNow);
            await auditRepo.AddAsync(new AuditEvent(
                shopId: shopId,
                entityType: EntityTypeRepairOrder,
                entityId: order.Id,
                action: "quote_cleared",
                actorUserId: userId,
                actorEmail: email,
                dataJson: JsonSerializer.Serialize(new { orderId = order.Id }),
                nowUtc: clock.UtcNow), ct);
        }
        else
        {
            order.SetQuote(body.Amount.Value, body.Currency ?? "", userId, clock.UtcNow);
            await auditRepo.AddAsync(new AuditEvent(
                shopId: shopId,
                entityType: EntityTypeRepairOrder,
                entityId: order.Id,
                action: "quote_set",
                actorUserId: userId,
                actorEmail: email,
                dataJson: JsonSerializer.Serialize(new { orderId = order.Id, amount = body.Amount, currency = body.Currency }),
                nowUtc: clock.UtcNow), ct);
        }

        await uow.SaveChangesAsync(ct);
        return Ok(new ApiResponse<RepairOrderResponse>(ToResponse(order)));
    }

    // PRO: payments
    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderPaymentResponse>>>> ListPayments(
        [FromServices] IRepairOrderPaymentRepository paymentsRepo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await paymentsRepo.ListByOrderAsync(shopId, id, ct);
        var res = list.Select(p => new RepairOrderPaymentResponse(p.Id, p.RepairOrderId, p.Amount, p.Currency, p.Method, p.Reference, p.CreatedByUserId, p.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<RepairOrderPaymentResponse>>(res));
    }

    [HttpPost("{id:guid}/payments")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<RepairOrderPaymentResponse>>> AddPayment(
        [FromServices] IRepairOrderRepository ordersRepo,
        [FromServices] IRepairOrderPaymentRepository paymentsRepo,
        [FromServices] IAuditEventRepository auditRepo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] CreateRepairOrderPaymentRequest body,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        var shopId = CurrentUser.GetShopId(User);
        var email = CurrentUser.GetEmail(User);
        if (userId == Guid.Empty || shopId == Guid.Empty) return Unauthorized();

        var order = await ordersRepo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        var payment = new RepairOrderPayment(shopId, id, body.Amount, body.Currency, body.Method, body.Reference, userId, clock.UtcNow);
        await paymentsRepo.AddAsync(payment, ct);

        await auditRepo.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeRepairOrder,
            entityId: order.Id,
            action: "payment_added",
            actorUserId: userId,
            actorEmail: email,
            dataJson: JsonSerializer.Serialize(new { orderId = order.Id, amount = payment.Amount, currency = payment.Currency, method = payment.Method.ToString(), reference = payment.Reference }),
            nowUtc: clock.UtcNow), ct);

        await uow.SaveChangesAsync(ct);

        var res = new RepairOrderPaymentResponse(payment.Id, payment.RepairOrderId, payment.Amount, payment.Currency, payment.Method, payment.Reference, payment.CreatedByUserId, payment.CreatedAtUtc);
        return Ok(new ApiResponse<RepairOrderPaymentResponse>(res));
    }

    // PRO: checklist
    [HttpGet("{id:guid}/checklist")]
    public async Task<ActionResult<ApiResponse<RepairOrderChecklistResponse>>> GetChecklist(
        [FromServices] IRepairOrderReceptionChecklistRepository checklistRepo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var cl = await checklistRepo.GetByOrderAsync(shopId, id, ct);
        if (cl is null) return NotFound();

        return Ok(new ApiResponse<RepairOrderChecklistResponse>(ToChecklistResponse(cl)));
    }

    [HttpPut("{id:guid}/checklist")]
    public async Task<ActionResult<ApiResponse<RepairOrderChecklistResponse>>> UpsertChecklist(
        [FromServices] IRepairOrderRepository ordersRepo,
        [FromServices] IRepairOrderReceptionChecklistRepository checklistRepo,
        [FromServices] IAuditEventRepository auditRepo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] UpdateRepairOrderChecklistRequest body,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        var shopId = CurrentUser.GetShopId(User);
        var email = CurrentUser.GetEmail(User);
        if (userId == Guid.Empty || shopId == Guid.Empty) return Unauthorized();

        var order = await ordersRepo.GetByIdAsync(shopId, id, ct);
        if (order is null) return NotFound();

        var cl = await checklistRepo.GetByOrderAsync(shopId, id, ct);
        if (cl is null)
        {
            cl = new RepairOrderReceptionChecklist(shopId, id, userId, clock.UtcNow);
            cl.Update(body.ScreenOk, body.CamerasOk, body.SpeakersOk, body.MicrophoneOk, body.ButtonsOk, body.FaceIdOk, body.FingerprintOk, body.CloudLock, body.BatteryPercent, body.CosmeticNotes, userId, clock.UtcNow);
            await checklistRepo.AddAsync(cl, ct);
        }
        else
        {
            cl.Update(body.ScreenOk, body.CamerasOk, body.SpeakersOk, body.MicrophoneOk, body.ButtonsOk, body.FaceIdOk, body.FingerprintOk, body.CloudLock, body.BatteryPercent, body.CosmeticNotes, userId, clock.UtcNow);
        }

        await auditRepo.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeRepairOrder,
            entityId: order.Id,
            action: "checklist_updated",
            actorUserId: userId,
            actorEmail: email,
            dataJson: JsonSerializer.Serialize(new { orderId = order.Id }),
            nowUtc: clock.UtcNow), ct);

        await uow.SaveChangesAsync(ct);
        return Ok(new ApiResponse<RepairOrderChecklistResponse>(ToChecklistResponse(cl)));
    }

    // PRO: audit
    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<ApiResponse<List<AuditEventResponse>>>> Audit(
        [FromServices] IAuditEventRepository auditRepo,
        Guid id,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var list = await auditRepo.ListByEntityAsync(shopId, EntityTypeRepairOrder, id, skip, take, ct);
        var res = list.Select(a => new AuditEventResponse(a.Id, a.EntityType, a.EntityId, a.Action, a.ActorUserId, a.ActorEmail, a.DataJson, a.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<AuditEventResponse>>(res));
    }

    // PRO: preview (render message template for this order)
    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<ApiResponse<MessagePreviewResponse>>> Preview(
        [FromServices] RenderOrderMessageService renderer,
        Guid id,
        [FromBody] RenderOrderMessageRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var preview = await renderer.RenderAsync(shopId, id, body.TemplateKey, allowFallback: false, ct);
        return Ok(new ApiResponse<MessagePreviewResponse>(preview));
    }

    private static RepairOrderResponse ToResponse(RepairOrder o)
        => new(
            o.Id,
            o.ShopId,
            o.CustomerId,
            o.DeviceId,
            o.IssueDescription,
            o.Notes,
            o.Status.ToString(),
            o.QuoteAmount,
            o.QuoteCurrency,
            o.QuoteUpdatedByUserId,
            o.QuoteUpdatedAtUtc,
            o.CreatedAtUtc,
            o.UpdatedAtUtc);

    private static RepairOrderChecklistResponse ToChecklistResponse(RepairOrderReceptionChecklist c)
        => new(
            c.Id,
            c.RepairOrderId,
            c.ScreenOk,
            c.CamerasOk,
            c.SpeakersOk,
            c.MicrophoneOk,
            c.ButtonsOk,
            c.FaceIdOk,
            c.FingerprintOk,
            c.CloudLock,
            c.BatteryPercent,
            c.CosmeticNotes,
            c.UpdatedByUserId,
            c.UpdatedAtUtc);
}
