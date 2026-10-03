using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.Inventory;
using RepairShop.Application.Notifications;
using RepairShop.Application.RepairOrders;

namespace RepairShop.Api.Controllers;

/// <summary>Notes, photos, checklists, parts and messages of an order.</summary>
[ApiController]
[Route("api/v1/orders/{orderId:guid}")]
[Authorize]
public sealed class OrderRecordsController : ControllerBase
{
    private readonly OrderRecordsService _records;

    public OrderRecordsController(OrderRecordsService records) => _records = records;

    // ===== Notes (internal or visible to the customer in the tracking portal) =====

    [HttpGet("notes")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderNoteResponse>>>> Notes(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.ListNotesAsync(CurrentUser.GetShopId(User), orderId, ct)));

    [HttpPost("notes")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<RepairOrderNoteResponse>>> AddNote(Guid orderId, [FromBody] CreateRepairOrderNoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.AddNoteAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    // ===== Attachments (photos / PDFs / links) =====

    [HttpGet("attachments")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderAttachmentResponse>>>> Attachments(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.ListAttachmentsAsync(CurrentUser.GetShopId(User), orderId, ct)));

    [HttpPost("attachments")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<RepairOrderAttachmentResponse>>> AddLink(Guid orderId, [FromBody] CreateRepairOrderAttachmentRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.AddLinkAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Uploads a photo (JPG/PNG/WEBP/HEIC) or PDF. Content type is validated from the file bytes.</summary>
    [HttpPost("attachments/upload")]
    [Authorize(Policy = Policies.OrdersWork)]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<RepairOrderAttachmentResponse>>> Upload(Guid orderId, IFormFile file, [FromForm] string? label, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(Envelope.Ok(await _records.UploadAsync(CurrentUser.GetShopId(User), orderId, stream, file.FileName, label, CurrentUser.GetActor(User), ct)));
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<IActionResult> DeleteAttachment(Guid orderId, Guid attachmentId, CancellationToken ct)
    {
        await _records.DeleteAttachmentAsync(CurrentUser.GetShopId(User), orderId, attachmentId, CurrentUser.GetActor(User), ct);
        return NoContent();
    }

    // ===== Checklists =====

    [HttpGet("checklist")]
    public async Task<ActionResult<ApiResponse<RepairOrderChecklistResponse?>>> Reception(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.GetReceptionAsync(CurrentUser.GetShopId(User), orderId, ct)));

    [HttpPut("checklist")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<RepairOrderChecklistResponse>>> SaveReception(Guid orderId, [FromBody] UpdateRepairOrderChecklistRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.UpsertReceptionAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Exit quality control. An approved QA is required to mark the order as Ready.</summary>
    [HttpGet("qa")]
    public async Task<ActionResult<ApiResponse<QaChecklistResponse?>>> Qa(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.GetQaAsync(CurrentUser.GetShopId(User), orderId, ct)));

    [HttpPut("qa")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<QaChecklistResponse>>> SaveQa(Guid orderId, [FromBody] UpdateQaChecklistRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _records.UpsertQaAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    // ===== Parts =====

    [HttpGet("parts")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderPartUsageResponse>>>> Parts([FromServices] InventoryService inventory, Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await inventory.ListByOrderAsync(CurrentUser.GetShopId(User), orderId, ct)));

    /// <summary>
    /// Consumes stock for the order. Parts reserved by the approved quote are used first (never charged twice);
    /// extra parts are charged to the customer when a unit price is given.
    /// </summary>
    [HttpPost("parts")]
    [Authorize(Policy = Policies.OrdersWork)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<List<RepairOrderPartUsageResponse>>>> UsePart([FromServices] InventoryService inventory, Guid orderId, [FromBody] UsePartOnOrderRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await inventory.UseOnOrderAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    [HttpGet("reservations")]
    public async Task<ActionResult<ApiResponse<List<ReservationResponse>>>> Reservations([FromServices] InventoryService inventory, Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await inventory.ListReservationsAsync(CurrentUser.GetShopId(User), orderId, ct)));

    // ===== Customer messages =====

    /// <summary>Renders a template for this order (preview + wa.me link) without sending it.</summary>
    [HttpGet("messages/preview")]
    public async Task<ActionResult<ApiResponse<MessagePreviewResponse>>> Preview([FromServices] RenderOrderMessageService renderer, Guid orderId, [FromQuery] string templateKey, CancellationToken ct)
        => Ok(Envelope.Ok(await renderer.RenderAsync(CurrentUser.GetShopId(User), orderId, templateKey, allowFallback: true, ct)));

    /// <summary>Queues a message to the customer through the configured provider (WhatsApp/SMS/email).</summary>
    [HttpPost("messages")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<NotificationResult>>> Send(
        [FromServices] NotificationService notifications, [FromServices] RepairOrderService orders, Guid orderId, [FromBody] SendOrderMessageRequest body, CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var order = await orders.RequireAsync(shopId, orderId, ct);
        var correlation = $"order:{orderId}:manual:{body.TemplateKey}:{Guid.NewGuid():N}";
        return Ok(Envelope.Ok(await notifications.NotifyOrderAsync(shopId, order, body.TemplateKey, correlation, true, body.Channel, CurrentUser.GetActor(User), ct, body.CustomBody)));
    }

    /// <summary>Records a message the staff sent by hand (e.g. after opening the wa.me link).</summary>
    [HttpPost("messages/manual")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> LogManual(
        [FromServices] NotificationService notifications, [FromServices] RepairOrderService orders, Guid orderId, [FromBody] LogManualMessageRequest body, CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var order = await orders.RequireAsync(shopId, orderId, ct);
        return Ok(Envelope.Ok(await notifications.LogManualAsync(shopId, order, body, CurrentUser.GetActor(User), ct)));
    }

    [HttpGet("messages")]
    public async Task<ActionResult<ApiResponse<List<NotificationResponse>>>> Messages([FromServices] INotificationOutboxRepository outbox, Guid orderId, CancellationToken ct)
    {
        var (items, _) = await outbox.SearchAsync(CurrentUser.GetShopId(User), null, NotificationService.EntityTypeRepairOrder, orderId, 0, 200, ct);
        return Ok(Envelope.Ok(items.Select(NotificationService.ToResponse).ToList()));
    }
}
