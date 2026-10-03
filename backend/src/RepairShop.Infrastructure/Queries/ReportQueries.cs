using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Sales;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Queries;

public sealed class ReportQueries : IReportQueries
{
    private static readonly RepairOrderStatus[] Final = { RepairOrderStatus.Delivered, RepairOrderStatus.Cancelled };

    private readonly RepairShopDbContext _db;
    public ReportQueries(RepairShopDbContext db) => _db = db;

    // Cross-shop (consolidated dashboard) needs to bypass the tenant filter: shop filters are explicit below.
    private IQueryable<RepairOrder> Orders(Guid shopId) => _db.RepairOrders.IgnoreQueryFilters().Where(o => o.ShopId == shopId);

    public async Task<IReadOnlyDictionary<RepairOrderStatus, int>> CountOrdersByStatusAsync(Guid shopId, CancellationToken ct)
        => (await Orders(shopId).GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);

    public Task<int> CountOverdueAsync(Guid shopId, DateTime nowUtc, CancellationToken ct)
        => Orders(shopId).CountAsync(o => o.PromisedAtUtc != null && o.PromisedAtUtc < nowUtc && !Final.Contains(o.Status) && o.Status != RepairOrderStatus.Ready, ct);

    public Task<int> CountStaleAsync(Guid shopId, DateTime olderThanUtc, CancellationToken ct)
        => Orders(shopId).CountAsync(o => !Final.Contains(o.Status) && o.Status != RepairOrderStatus.Ready && o.LastStatusChangeAtUtc < olderThanUtc, ct);

    public Task<int> CountReadyBeforeAsync(Guid shopId, DateTime readyBeforeUtc, CancellationToken ct)
        => Orders(shopId).CountAsync(o => o.Status == RepairOrderStatus.Ready && o.ReadyAtUtc != null && o.ReadyAtUtc < readyBeforeUtc, ct);

    public Task<int> CountQuotesAsync(Guid shopId, QuoteStatus status, CancellationToken ct)
        => _db.Quotes.IgnoreQueryFilters().CountAsync(q => q.ShopId == shopId && q.Status == status, ct);

    public Task<int> CountLowStockAsync(Guid shopId, CancellationToken ct)
        => _db.InventoryItems.IgnoreQueryFilters().CountAsync(i => i.ShopId == shopId && i.IsActive && i.TrackStock &&
            i.QuantityOnHand - _db.InventoryReservations.IgnoreQueryFilters()
                .Where(r => r.InventoryItemId == i.Id && r.Status == ReservationStatus.Active)
                .Sum(r => (int?)(r.Quantity - r.ConsumedQuantity) ?? 0) <= i.MinStock, ct);

    public Task<int> CountOpenAssignedAsync(Guid shopId, Guid userId, CancellationToken ct)
        => Orders(shopId).CountAsync(o => o.AssignedTechnicianId == userId && !Final.Contains(o.Status), ct);

    public Task<bool> HasOpenCashSessionAsync(Guid shopId, CancellationToken ct)
        => _db.CashSessions.IgnoreQueryFilters().AnyAsync(s => s.ShopId == shopId && s.Status == CashSessionStatus.Open, ct);

    public async Task<IReadOnlyList<WorkloadRow>> TechnicianWorkloadAsync(Guid shopId, DateTime nowUtc, CancellationToken ct)
    {
        var rows = await Orders(shopId)
            .Where(o => o.AssignedTechnicianId != null && !Final.Contains(o.Status))
            .GroupBy(o => o.AssignedTechnicianId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Open = g.Count(),
                Overdue = g.Count(o => o.PromisedAtUtc != null && o.PromisedAtUtc < nowUtc && o.Status != RepairOrderStatus.Ready)
            })
            .ToListAsync(ct);

        var techIds = await _db.Users
            .Where(u => u.IsActive && (u.ShopId == shopId && (u.Role == UserRole.Tech || u.Role == UserRole.Admin)
                                       || _db.UserShopAccess.Any(a => a.UserId == u.Id && a.ShopId == shopId && (a.Role == UserRole.Tech || a.Role == UserRole.Admin))))
            .Select(u => new { u.Id, u.DisplayName, u.Role })
            .ToListAsync(ct);

        var names = techIds.ToDictionary(u => u.Id, u => u.DisplayName);
        foreach (var r in rows.Where(r => !names.ContainsKey(r.UserId)))
            names[r.UserId] = (await _db.Users.Where(u => u.Id == r.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)) ?? "?";

        return names.Select(n =>
        {
            var row = rows.FirstOrDefault(r => r.UserId == n.Key);
            return new WorkloadRow(n.Key, n.Value, row?.Open ?? 0, row?.Overdue ?? 0);
        }).Where(w => w.OpenOrders > 0 || techIds.Any(t => t.Id == w.UserId && t.Role == UserRole.Tech))
          .OrderByDescending(w => w.OpenOrders).ToList();
    }

    public async Task<IReadOnlyList<CurrencyAmount>> OrderPaymentsByCurrencyAsync(Guid shopId, CancellationToken ct)
        => (await _db.RepairOrderPayments.IgnoreQueryFilters().Where(p => p.ShopId == shopId)
                .GroupBy(p => new { p.Currency, p.Type })
                .Select(g => new { g.Key.Currency, g.Key.Type, Total = g.Sum(x => x.Amount) })
                .ToListAsync(ct))
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyAmount(g.Key, decimal.Round(g.Sum(x => x.Type == PaymentType.Refund ? -x.Total : x.Total), 2)))
            .ToList();

    public async Task<IReadOnlyList<MoneyMovementRow>> MoneyMovementsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var payments = await _db.RepairOrderPayments.IgnoreQueryFilters()
            .Where(p => p.ShopId == shopId && p.CreatedAtUtc >= fromUtc && p.CreatedAtUtc <= toUtc)
            .Select(p => new { p.CreatedAtUtc, p.Type, p.Currency, p.Method, p.Amount })
            .ToListAsync(ct);

        var sales = await _db.Sales.IgnoreQueryFilters()
            .Where(s => s.ShopId == shopId && s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc <= toUtc)
            .Select(s => new { s.CreatedAtUtc, s.Currency, s.ChangeAmount, Payments = s.Payments.Select(p => new { p.Method, p.Amount }).ToList() })
            .ToListAsync(ct);

        var refunds = await _db.SaleRefunds
            .Where(r => r.CreatedAtUtc >= fromUtc && r.CreatedAtUtc <= toUtc && _db.Sales.IgnoreQueryFilters().Any(s => s.Id == r.SaleId && s.ShopId == shopId))
            .Select(r => new { r.CreatedAtUtc, r.Method, r.Amount, Currency = _db.Sales.IgnoreQueryFilters().Where(s => s.Id == r.SaleId).Select(s => s.Currency).First() })
            .ToListAsync(ct);

        var rows = new List<MoneyMovementRow>();
        rows.AddRange(payments.Select(p => new MoneyMovementRow(p.CreatedAtUtc, p.Type == PaymentType.Refund ? "order_refund" : "order_payment", p.Currency, p.Method.ToString(),
            p.Type == PaymentType.Refund ? -p.Amount : p.Amount)));

        foreach (var s in sales)
        {
            var change = s.ChangeAmount;
            foreach (var p in s.Payments)
            {
                var amount = p.Amount;
                if (p.Method == PaymentMethod.Cash && change > 0)
                {
                    var deduct = Math.Min(change, amount);
                    amount -= deduct;
                    change -= deduct;
                }
                if (amount != 0) rows.Add(new MoneyMovementRow(s.CreatedAtUtc, "sale", s.Currency, p.Method.ToString(), amount));
            }
        }

        rows.AddRange(refunds.Select(r => new MoneyMovementRow(r.CreatedAtUtc, "sale_refund", r.Currency, r.Method.ToString(), -r.Amount)));
        return rows.OrderBy(r => r.AtUtc).ToList();
    }

    public async Task<IReadOnlyList<OrderFactRow>> OrderFactsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, bool byDelivery, CancellationToken ct)
    {
        var orders = Orders(shopId);
        orders = byDelivery
            ? orders.Where(o => o.DeliveredAtUtc != null && o.DeliveredAtUtc >= fromUtc && o.DeliveredAtUtc <= toUtc)
            : orders.Where(o => o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc <= toUtc);

        var rows = await (
            from o in orders
            join d in _db.Devices.IgnoreQueryFilters() on o.DeviceId equals d.Id
            join c in _db.Customers.IgnoreQueryFilters() on o.CustomerId equals c.Id
            join u in _db.Users on o.AssignedTechnicianId equals u.Id into tech
            from t in tech.DefaultIfEmpty()
            select new
            {
                o.Id, o.OrderNumber, o.Status, o.IssueCategory, d.Brand, d.Model, o.AssignedTechnicianId, TechName = t != null ? t.DisplayName : null,
                o.QuoteAmount, o.QuoteCurrency, o.IsWarrantyClaim, o.WarrantyOfOrderId, o.CreatedAtUtc, o.ReadyAtUtc, o.DeliveredAtUtc, c.FullName,
                Extra = _db.RepairOrderPartUsages.IgnoreQueryFilters().Where(x => x.RepairOrderId == o.Id && x.ChargedToCustomer && x.UnitPrice != null)
                    .Sum(x => (decimal?)(x.UnitPrice!.Value * x.QuantityUsed)) ?? 0m
            }).ToListAsync(ct);

        return rows.Select(r => new OrderFactRow(r.Id, r.OrderNumber, r.Status, r.IssueCategory, r.Brand, r.Model, r.AssignedTechnicianId, r.TechName,
            r.QuoteAmount, r.QuoteCurrency, decimal.Round(r.Extra, 2), r.IsWarrantyClaim, r.WarrantyOfOrderId, r.CreatedAtUtc, r.ReadyAtUtc, r.DeliveredAtUtc, r.FullName)).ToList();
    }

    public async Task<IReadOnlyList<StatusChangeRow>> StatusChangesAsync(Guid shopId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return Array.Empty<StatusChangeRow>();
        var ids = orderIds.ToList();
        return await _db.RepairOrderStatusHistory.IgnoreQueryFilters()
            .Where(h => h.ShopId == shopId && ids.Contains(h.RepairOrderId))
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new StatusChangeRow(h.RepairOrderId, h.FromStatus, h.ToStatus, h.ChangedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SaleFactRow>> SaleFactsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        => await _db.Sales.IgnoreQueryFilters()
            .Where(s => s.ShopId == shopId && s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc <= toUtc)
            .Select(s => new SaleFactRow(s.Id, s.Number, s.CreatedAtUtc, s.Currency, s.Total, s.RefundedAmount,
                s.Lines.Sum(l => (decimal?)((l.UnitCost ?? 0) * (l.Quantity - l.RefundedQuantity))) ?? 0m,
                s.Status == SaleStatus.Voided))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QuoteFactRow>> QuoteFactsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        => await _db.Quotes.IgnoreQueryFilters()
            .Where(q => q.ShopId == shopId && q.CreatedAtUtc >= fromUtc && q.CreatedAtUtc <= toUtc)
            .Select(q => new QuoteFactRow(q.Id, q.Status, q.Currency, q.Total, q.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PartUseRow>> PartUsesAsync(Guid shopId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return Array.Empty<PartUseRow>();
        var ids = orderIds.ToList();
        return await (
            from u in _db.RepairOrderPartUsages.IgnoreQueryFilters()
            where u.ShopId == shopId && ids.Contains(u.RepairOrderId)
            join i in _db.InventoryItems.IgnoreQueryFilters() on u.InventoryItemId equals i.Id
            select new PartUseRow(u.RepairOrderId, i.Id, i.Sku, i.Name, u.QuantityUsed, u.UnitCost, u.UnitCostCurrency)
        ).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FeedbackRow>> FeedbackAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        => await (
            from f in _db.CustomerFeedback.IgnoreQueryFilters()
            where f.ShopId == shopId && f.CreatedAtUtc >= fromUtc && f.CreatedAtUtc <= toUtc
            join o in _db.RepairOrders.IgnoreQueryFilters() on f.RepairOrderId equals o.Id
            select new FeedbackRow(o.OrderNumber, f.Score, f.Comment, f.CreatedAtUtc)
        ).ToListAsync(ct);

    public async Task<IReadOnlyList<StockRow>> StockAsync(Guid shopId, CancellationToken ct)
        => await _db.InventoryItems.IgnoreQueryFilters()
            .Where(i => i.ShopId == shopId && i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new StockRow(i.Id, i.Sku, i.Name, i.Category, i.QuantityOnHand,
                _db.InventoryReservations.IgnoreQueryFilters().Where(r => r.InventoryItemId == i.Id && r.Status == ReservationStatus.Active).Sum(r => (int?)(r.Quantity - r.ConsumedQuantity)) ?? 0,
                i.MinStock, i.TrackStock, i.UnitCost, i.UnitCostCurrency))
            .ToListAsync(ct);
}
