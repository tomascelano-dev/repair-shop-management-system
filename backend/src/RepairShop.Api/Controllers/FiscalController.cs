using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Currency;
using RepairShop.Application.Documents;
using RepairShop.Application.Fiscal;

namespace RepairShop.Api.Controllers;

/// <summary>Electronic invoices (ARCA) and exchange rates.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class FiscalController : ControllerBase
{
    [HttpGet("invoices")]
    [Authorize(Policy = Policies.Sales)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FiscalInvoiceResponse>>>> Invoices([FromServices] FiscalService fiscal,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(Envelope.Ok(Response.WithTotal(await fiscal.SearchAsync(CurrentUser.GetShopId(User), dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), skip, take, ct))));

    [HttpGet("invoices/{id:guid}")]
    [Authorize(Policy = Policies.Sales)]
    public async Task<ActionResult<ApiResponse<FiscalInvoiceResponse>>> Invoice([FromServices] FiscalService fiscal, Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await fiscal.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpGet("invoices/{id:guid}/pdf")]
    [Authorize(Policy = Policies.Sales)]
    public async Task<IActionResult> InvoicePdf([FromServices] DocumentService docs, Guid id, CancellationToken ct)
    {
        var doc = await docs.InvoiceAsync(CurrentUser.GetShopId(User), id, ct);
        Response.Headers.ContentDisposition = $"inline; filename=\"{doc.FileName}\"";
        return File(doc.Content, doc.ContentType);
    }

    /// <summary>Issues the credit note that cancels an authorized invoice.</summary>
    [HttpPost("invoices/{id:guid}/credit-note")]
    [Authorize(Policy = Policies.AdminOnly)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<FiscalInvoiceResponse>>> CreditNote([FromServices] FiscalService fiscal, Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await fiscal.CreditNoteAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));

    [HttpGet("exchange-rates")]
    public async Task<ActionResult<ApiResponse<List<ExchangeRateResponse>>>> Rates([FromServices] ExchangeRateService rates, [FromQuery] int take = 30, CancellationToken ct = default)
        => Ok(Envelope.Ok(await rates.ListRecentAsync(Math.Clamp(take, 1, 365), ct)));

    /// <summary>Sets the rate of a day manually (e.g. the rate the shop actually used).</summary>
    [HttpPut("exchange-rates")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<ExchangeRateResponse>>> SetRate([FromServices] ExchangeRateService rates, [FromBody] SetExchangeRateRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await rates.SetAsync(body, ct)));

    /// <summary>Fetches today's rates from the configured provider (dolarapi.com by default).</summary>
    [HttpPost("exchange-rates/refresh")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<int>>> Refresh([FromServices] ExchangeRateService rates, CancellationToken ct)
        => Ok(Envelope.Ok(await rates.RefreshFromProviderAsync(ct)));
}
