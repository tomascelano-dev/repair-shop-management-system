using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Reports;

namespace RepairShop.Api.Controllers;

/// <summary>Management reports. Amounts are reported per currency and converted with the rate of each day.</summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = Policies.Reports)]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports) => _reports = reports;

    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResponse<RevenueReport>>> Revenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string groupBy = "day", CancellationToken ct = default)
        => Ok(Envelope.Ok(await _reports.RevenueAsync(CurrentUser.GetShopId(User), from, to, groupBy, ct)));

    [HttpGet("margins")]
    public async Task<ActionResult<ApiResponse<MarginReport>>> Margins([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.MarginsAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("repair-times")]
    public async Task<ActionResult<ApiResponse<RepairTimesReport>>> RepairTimes([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.RepairTimesAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("quotes")]
    public async Task<ActionResult<ApiResponse<QuoteStatsReport>>> Quotes([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.QuotesAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("top-issues")]
    public async Task<ActionResult<ApiResponse<TopIssuesReport>>> TopIssues([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.TopIssuesAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("technicians")]
    public async Task<ActionResult<ApiResponse<TechniciansReport>>> Technicians([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.TechniciansAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("warranty")]
    public async Task<ActionResult<ApiResponse<WarrantyReport>>> Warranty([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.WarrantyAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("feedback")]
    public async Task<ActionResult<ApiResponse<FeedbackReport>>> Feedback([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.FeedbackAsync(CurrentUser.GetShopId(User), from, to, ct)));

    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<InventoryReport>>> Inventory(CancellationToken ct)
        => Ok(Envelope.Ok(await _reports.InventoryAsync(CurrentUser.GetShopId(User), ct)));

    /// <summary>Excel export of any report (revenue, margins, repair-times, quotes, top-issues, technicians, warranty, feedback, inventory).</summary>
    [HttpGet("{report}/export")]
    public async Task<IActionResult> Export(string report, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? groupBy, CancellationToken ct)
    {
        var bytes = await _reports.ExportAsync(CurrentUser.GetShopId(User), report, from, to, groupBy, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"reporte-{report}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
