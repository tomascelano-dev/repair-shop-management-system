using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryResponse>>> Summary(
        [FromServices] RepairShopDbContext db,
        [FromServices] IDateTimeProvider clock,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var orders = db.RepairOrders.Where(x => x.ShopId == shopId);

        var totalOrders = await orders.CountAsync(ct);
        var openOrders = await orders.CountAsync(x => x.Status != RepairOrderStatus.Delivered && x.Status != RepairOrderStatus.Cancelled, ct);
        var readyOrders = await orders.CountAsync(x => x.Status == RepairOrderStatus.Ready, ct);
        var deliveredOrders = await orders.CountAsync(x => x.Status == RepairOrderStatus.Delivered, ct);
        var cancelledOrders = await orders.CountAsync(x => x.Status == RepairOrderStatus.Cancelled, ct);

        var payments = db.RepairOrderPayments.Where(p => p.ShopId == shopId);
        var paymentsByCurrency = await payments
            .GroupBy(p => p.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        string? currency = null;
        decimal totalPayments = 0m;
        if (paymentsByCurrency.Count == 1)
        {
            currency = paymentsByCurrency[0].Currency;
            totalPayments = paymentsByCurrency[0].Total;
        }
        else if (paymentsByCurrency.Count > 1)
        {
            // Mixed currencies -> return null currency and 0 to avoid misleading totals
            currency = null;
            totalPayments = 0m;
        }

        var res = new DashboardSummaryResponse(
            ShopId: shopId,
            TotalOrders: totalOrders,
            OpenOrders: openOrders,
            ReadyOrders: readyOrders,
            DeliveredOrders: deliveredOrders,
            CancelledOrders: cancelledOrders,
            TotalPaymentsAmount: decimal.Round(totalPayments, 2, MidpointRounding.AwayFromZero),
            PaymentsCurrency: currency,
            GeneratedAtUtc: clock.UtcNow);

        return Ok(new ApiResponse<DashboardSummaryResponse>(res));
    }
}
