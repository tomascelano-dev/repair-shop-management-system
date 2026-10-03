using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Cash;
using RepairShop.Application.Contracts;
using RepairShop.Application.Documents;

namespace RepairShop.Api.Controllers;

/// <summary>Cash register: opening, income/expenses/withdrawals and closing count (arqueo) per payment method.</summary>
[ApiController]
[Route("api/v1/cash")]
[Authorize(Policy = Policies.Sales)]
public sealed class CashController : ControllerBase
{
    private readonly CashRegisterService _cash;

    public CashController(CashRegisterService cash) => _cash = cash;

    /// <summary>The open session of this branch (null when the register is closed).</summary>
    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<CashSessionResponse?>>> Current(CancellationToken ct)
        => Ok(Envelope.Ok(await _cash.GetCurrentAsync(CurrentUser.GetShopId(User), ct)));

    [HttpGet("sessions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashSessionResponse>>>> Sessions([FromQuery] int skip = 0, [FromQuery] int take = 30, CancellationToken ct = default)
        => Ok(Envelope.Ok(Response.WithTotal(await _cash.ListAsync(CurrentUser.GetShopId(User), skip, take, ct))));

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CashSessionResponse>>> Session(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _cash.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpPost("open")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<CashSessionResponse>>> Open([FromBody] OpenCashSessionRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _cash.OpenAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPost("close")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<CashSessionResponse>>> Close([FromBody] CloseCashSessionRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _cash.CloseAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    /// <summary>Manual movement: income, expense (with category) or withdrawal.</summary>
    [HttpPost("movements")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<CashMovementResponse>>> Movement([FromBody] CreateCashMovementRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _cash.AddManualMovementAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpGet("sessions/{id:guid}/report")]
    public async Task<IActionResult> Report([FromServices] DocumentService docs, Guid id, CancellationToken ct)
    {
        var doc = await docs.CashReportAsync(CurrentUser.GetShopId(User), id, ct);
        Response.Headers.ContentDisposition = $"inline; filename=\"{doc.FileName}\"";
        return File(doc.Content, doc.ContentType);
    }
}
