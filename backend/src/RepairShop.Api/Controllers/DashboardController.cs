using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Admin;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Reports;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Operational summary of the branch (computed in a few aggregated queries).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryResponse>>> Summary(CancellationToken ct)
        => Ok(Envelope.Ok(await _dashboard.GetSummaryAsync(CurrentUser.GetShopId(User), CurrentUser.GetUserId(User), ct)));

    /// <summary>Daily revenue (orders + counter sales - refunds) converted to the reporting currency.</summary>
    [HttpGet("revenue")]
    [Authorize(Policy = Policies.Reports)]
    public async Task<ActionResult<ApiResponse<List<RevenuePoint>>>> Revenue([FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(Envelope.Ok(await _dashboard.GetRevenueSeriesAsync(CurrentUser.GetShopId(User), Math.Clamp(days, 7, 366), ct)));

    /// <summary>All branches the user can access, side by side (multi-sucursal).</summary>
    [HttpGet("consolidated")]
    [Authorize(Policy = Policies.Reports)]
    public async Task<ActionResult<ApiResponse<ConsolidatedDashboardResponse>>> Consolidated(
        [FromServices] ShopSettingsService settings, [FromServices] IShopRepository shops, CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var current = await shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var branches = (await settings.ListBranchesAsync(shopId, CurrentUser.GetUserId(User), ct))
            .Where(b => b.IsActive && b.MyRole is not null)
            .Select(b => (b.Id, b.Name))
            .ToList();

        return Ok(Envelope.Ok(await _dashboard.GetConsolidatedAsync(branches, current.ReportingCurrency, ct)));
    }
}
