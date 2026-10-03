using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.Documents;
using RepairShop.Application.Fiscal;
using RepairShop.Application.Sales;
using RepairShop.Domain.Sales;

namespace RepairShop.Api.Controllers;

/// <summary>
/// Point of sale: counter sales of accessories and parts with split payments, discounts, change,
/// refunds (with or without restock) and voids. Each sale moves stock and the open cash register atomically.
/// </summary>
[ApiController]
[Route("api/v1/sales")]
[Authorize(Policy = Policies.Sales)]
public sealed class SalesController : ControllerBase
{
    private readonly SalesService _sales;

    public SalesController(SalesService sales) => _sales = sales;

    /// <summary>Sellable items for the POS grid/search (name, SKU or barcode).</summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<ApiResponse<List<PosCatalogItem>>>> Catalog([FromQuery] string? q, [FromQuery] int take = 60, CancellationToken ct = default)
        => Ok(Envelope.Ok(await _sales.CatalogAsync(CurrentUser.GetShopId(User), q, take, ct)));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SaleResponse>>>> List(
        [FromQuery] string? q, [FromQuery] SaleStatus? status, [FromQuery] Guid? customerId, [FromQuery] Guid? cashSessionId,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var options = new SaleSearchOptions(q, status, customerId, cashSessionId, dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), skip, take);
        return Ok(Envelope.Ok(Response.WithTotal(await _sales.SearchAsync(CurrentUser.GetShopId(User), options, ct))));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SaleResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _sales.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    /// <summary>Checkout. Send an Idempotency-Key so a double click or a retry never charges twice.</summary>
    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<SaleResponse>>> Create([FromBody] CreateSaleRequest body, CancellationToken ct)
    {
        var sale = await _sales.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct);
        return CreatedAtAction(nameof(Get), new { id = sale.Id }, Envelope.Ok(sale));
    }

    [HttpPost("{id:guid}/refund")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<SaleResponse>>> Refund(Guid id, [FromBody] RefundSaleRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _sales.RefundAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Cancels the whole sale (admin): restocks and returns the money from the register.</summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<SaleResponse>>> Void(Guid id, [FromBody] VoidSaleRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _sales.VoidAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Printable ticket (80 mm PDF).</summary>
    [HttpGet("{id:guid}/ticket")]
    public async Task<IActionResult> Ticket([FromServices] DocumentService docs, Guid id, CancellationToken ct)
    {
        var doc = await docs.SaleTicketAsync(CurrentUser.GetShopId(User), id, ct);
        Response.Headers.ContentDisposition = $"inline; filename=\"{doc.FileName}\"";
        return File(doc.Content, doc.ContentType);
    }

    [HttpGet("{id:guid}/invoices")]
    public async Task<ActionResult<ApiResponse<List<FiscalInvoiceResponse>>>> Invoices([FromServices] FiscalService fiscal, Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await fiscal.ListBySourceAsync(CurrentUser.GetShopId(User), FiscalService.SourceSale, id, ct)));

    [HttpPost("{id:guid}/invoices")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<FiscalInvoiceResponse>>> Invoice([FromServices] FiscalService fiscal, Guid id, [FromBody] IssueInvoiceRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await fiscal.IssueForSaleAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));
}
