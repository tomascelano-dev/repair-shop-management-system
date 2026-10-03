using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.Documents;
using RepairShop.Application.RepairOrders;
using RepairShop.Application.Suggestions;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly RepairOrderService _orders;

    public OrdersController(RepairOrderService orders) => _orders = orders;

    /// <summary>
    /// Search orders. q matches the order number (#123), customer name/phone, device brand/model/serial/IMEI and
    /// the issue. Paged with X-Total-Count.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RepairOrderResponse>>>> List(
        [FromQuery] string? q,
        [FromQuery] RepairOrderStatus? status,
        [FromQuery] string? statuses,
        [FromQuery] Guid? technicianId,
        [FromQuery] bool? mine,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? deviceId,
        [FromQuery] bool? onlyOpen,
        [FromQuery] bool? onlyOverdue,
        [FromQuery] RepairOrderPriority? priority,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var statusList = ParseStatuses(statuses);
        var options = new RepairOrderSearchOptions(
            Q: q,
            Status: status,
            DateFromUtc: dateFrom?.ToUniversalTime(),
            DateToUtc: dateTo?.ToUniversalTime(),
            SortBy: sortBy,
            SortDir: sortDir,
            Skip: skip,
            Take: take,
            AssignedTechnicianId: mine == true ? CurrentUser.GetUserId(User) : technicianId,
            CustomerId: customerId,
            DeviceId: deviceId,
            OnlyOpen: onlyOpen,
            OnlyOverdue: onlyOverdue,
            Priority: priority,
            Statuses: statusList,
            NowUtc: DateTime.UtcNow);

        return Ok(Envelope.Ok(Response.WithTotal(await _orders.SearchAsync(CurrentUser.GetShopId(User), options, ct))));
    }

    /// <summary>Kanban board: open orders grouped by status (delivered/cancelled excluded).</summary>
    [HttpGet("board")]
    public async Task<ActionResult<ApiResponse<OrderBoardResponse>>> Board([FromQuery] string? q, [FromQuery] Guid? technicianId, [FromQuery] bool? mine, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.GetBoardAsync(CurrentUser.GetShopId(User), q, mine == true ? CurrentUser.GetUserId(User) : technicianId, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpPost]
    [Authorize(Policy = Policies.OrdersManage)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Create([FromBody] RepairOrderCreateRequest body, CancellationToken ct)
    {
        var created = await _orders.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, Envelope.Ok(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Update(Guid id, [FromBody] RepairOrderUpdateRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.UpdateAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Only orders without payments, parts or quotes can be deleted (otherwise cancel them).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _orders.DeleteAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct);
        return NoContent();
    }

    /// <summary>Technician, priority and promised date.</summary>
    [HttpPut("{id:guid}/plan")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Plan(Guid id, [FromBody] PlanOrderRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.PlanAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Quick agreed price (without itemized quote). Itemized quotes live under /quotes.</summary>
    [HttpPut("{id:guid}/quote")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> SetAgreedPrice(Guid id, [FromBody] SetOrderQuoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.SetAgreedPriceAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>
    /// Moves the order through the workflow (see docs/state-machine.md). Returns the suggested customer
    /// message, the queued notification (if any) and a WhatsApp link.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<ChangeOrderStatusResponse>>> ChangeStatus(
        [FromServices] ChangeOrderStatusService statusService, Guid id, [FromBody] ChangeOrderStatusRequest body, CancellationToken ct)
    {
        var role = CurrentUser.GetRole(User);
        var permissions = role is null ? Array.Empty<string>() : RepairShop.Application.Security.Permissions.For(role.Value);
        if (!permissions.Contains(RepairShop.Application.Security.Permissions.OrdersWork) && !permissions.Contains(RepairShop.Application.Security.Permissions.Sales))
            return Forbid();

        return Ok(Envelope.Ok(await statusService.HandleAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<List<OrderStatusHistoryResponse>>>> History([FromServices] OrderRecordsService records, Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await records.ListHistoryAsync(CurrentUser.GetShopId(User), id, ct)));

    /// <summary>Opens a no-charge warranty re-entry linked to the original (delivered) order.</summary>
    [HttpPost("{id:guid}/warranty-claim")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> WarrantyClaim(Guid id, [FromBody] WarrantyClaimRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.CreateWarrantyClaimAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Invalidates the current tracking link (e.g. it was shared by mistake).</summary>
    [HttpPost("{id:guid}/tracking-token")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> RegenerateTrackingToken(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.RegenerateTrackingTokenAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));

    /// <summary>Stores the device unlock code/pattern encrypted. It's purged automatically on delivery.</summary>
    [HttpPut("{id:guid}/unlock")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<IActionResult> SetUnlock(Guid id, [FromBody] SetUnlockSecretRequest body, CancellationToken ct)
    {
        await _orders.SetUnlockSecretAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct);
        return NoContent();
    }

    /// <summary>Reveals the unlock code (audited).</summary>
    [HttpPost("{id:guid}/unlock/reveal")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<UnlockSecretResponse>>> RevealUnlock(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(Envelope.Ok(await _orders.RevealUnlockSecretAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));
    }

    /// <summary>Customer signature on the intake (reception) or on delivery, drawn on screen.</summary>
    [HttpPost("{id:guid}/signatures")]
    [Authorize(Policy = Policies.OrdersManage)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<RepairOrderResponse>>> Signature(Guid id, [FromBody] SaveSignatureRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _orders.SaveSignatureAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Similar past repairs, suggested quote items and price range (+ optional AI hypotheses).</summary>
    [HttpGet("{id:guid}/suggestions")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<RepairSuggestionResponse>>> Suggestions([FromServices] SuggestionService suggestions, Guid id, [FromQuery] bool ai = false, CancellationToken ct = default)
        => Ok(Envelope.Ok(await suggestions.GetAsync(CurrentUser.GetShopId(User), id, ai, ct)));

    // ===== Printable documents (PDF) =====

    [HttpGet("{id:guid}/documents/intake")]
    public async Task<IActionResult> IntakeReceipt([FromServices] DocumentService docs, Guid id, CancellationToken ct)
        => Pdf(await docs.IntakeReceiptAsync(CurrentUser.GetShopId(User), id, ct));

    [HttpGet("{id:guid}/documents/label")]
    public async Task<IActionResult> Label([FromServices] DocumentService docs, Guid id, CancellationToken ct)
        => Pdf(await docs.LabelAsync(CurrentUser.GetShopId(User), id, ct));

    [HttpGet("{id:guid}/documents/quote")]
    public async Task<IActionResult> QuotePdf([FromServices] DocumentService docs, Guid id, [FromQuery] Guid? quoteId, CancellationToken ct)
        => Pdf(await docs.QuoteAsync(CurrentUser.GetShopId(User), id, quoteId, ct));

    [HttpGet("{id:guid}/documents/warranty")]
    public async Task<IActionResult> Warranty([FromServices] DocumentService docs, Guid id, CancellationToken ct)
        => Pdf(await docs.WarrantyCertificateAsync(CurrentUser.GetShopId(User), id, ct));

    [HttpGet("{id:guid}/documents/payments/{paymentId:guid}")]
    public async Task<IActionResult> PaymentReceipt([FromServices] DocumentService docs, Guid id, Guid paymentId, CancellationToken ct)
        => Pdf(await docs.PaymentReceiptAsync(CurrentUser.GetShopId(User), id, paymentId, ct));

    private FileContentResult Pdf(GeneratedDocument doc)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = $"inline; filename=\"{doc.FileName}\"";
        return File(doc.Content, doc.ContentType);
    }

    private static IReadOnlyCollection<RepairOrderStatus>? ParseStatuses(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var list = new List<RepairOrderStatus>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<RepairOrderStatus>(part, true, out var s) && Enum.IsDefined(s)) list.Add(s);
        }
        return list.Count == 0 ? null : list;
    }
}
