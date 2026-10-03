using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Queries;

public sealed class RepairOrderReadModel : IRepairOrderReadModel
{
    private readonly RepairShopDbContext _db;
    public RepairOrderReadModel(RepairShopDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, OrderReadInfo>> GetInfoAsync(Guid shopId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return new Dictionary<Guid, OrderReadInfo>();
        var ids = orderIds.Distinct().ToList();

        var basics = await (
            from o in _db.RepairOrders.IgnoreQueryFilters()
            where o.ShopId == shopId && ids.Contains(o.Id)
            join c in _db.Customers.IgnoreQueryFilters() on o.CustomerId equals c.Id
            join d in _db.Devices.IgnoreQueryFilters() on o.DeviceId equals d.Id
            join u in _db.Users on o.AssignedTechnicianId equals u.Id into tech
            from t in tech.DefaultIfEmpty()
            select new
            {
                o.Id,
                c.FullName,
                c.Phone,
                c.Email,
                c.NotificationsOptIn,
                d.Brand,
                d.Model,
                d.Label,
                d.SerialNumber,
                d.Imei,
                TechName = t != null ? t.DisplayName : null
            }).ToListAsync(ct);

        var payments = await _db.RepairOrderPayments.IgnoreQueryFilters()
            .Where(p => p.ShopId == shopId && ids.Contains(p.RepairOrderId))
            .GroupBy(p => new { p.RepairOrderId, p.Type, p.Currency })
            .Select(g => new { g.Key.RepairOrderId, g.Key.Type, g.Key.Currency, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var extras = await _db.RepairOrderPartUsages.IgnoreQueryFilters()
            .Where(u => u.ShopId == shopId && ids.Contains(u.RepairOrderId) && u.ChargedToCustomer && u.UnitPrice != null)
            .GroupBy(u => u.RepairOrderId)
            .Select(g => new { OrderId = g.Key, Total = g.Sum(x => x.UnitPrice!.Value * x.QuantityUsed) })
            .ToListAsync(ct);

        var quotes = await _db.Quotes.IgnoreQueryFilters()
            .Where(q => q.ShopId == shopId && ids.Contains(q.RepairOrderId) && q.Status != QuoteStatus.Superseded)
            .Select(q => new { q.RepairOrderId, q.Status })
            .ToListAsync(ct);

        var qa = await _db.RepairOrderQaChecklists.IgnoreQueryFilters()
            .Where(q => q.ShopId == shopId && ids.Contains(q.RepairOrderId))
            .Select(q => new { q.RepairOrderId, q.Passed })
            .ToListAsync(ct);

        var photos = await _db.RepairOrderAttachments.IgnoreQueryFilters()
            .Where(a => a.ShopId == shopId && ids.Contains(a.RepairOrderId) && a.Kind == AttachmentKind.Photo)
            .GroupBy(a => a.RepairOrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, OrderReadInfo>();
        foreach (var b in basics)
        {
            var p = payments.Where(x => x.RepairOrderId == b.Id).ToList();
            var paid = p.Where(x => x.Type == PaymentType.Payment).Sum(x => x.Total) - p.Where(x => x.Type == PaymentType.Refund).Sum(x => x.Total);
            var q = quotes.Where(x => x.RepairOrderId == b.Id).ToList();

            result[b.Id] = new OrderReadInfo(
                b.Id, b.FullName, b.Phone, b.Email, b.NotificationsOptIn, b.Brand, b.Model, b.Label, b.SerialNumber, b.Imei, b.TechName,
                decimal.Round(paid, 2), p.Select(x => x.Currency).FirstOrDefault(),
                decimal.Round(extras.FirstOrDefault(x => x.OrderId == b.Id)?.Total ?? 0m, 2),
                q.Any(x => x.Status == QuoteStatus.Approved),
                q.Any(x => x.Status is QuoteStatus.Draft or QuoteStatus.Sent),
                qa.FirstOrDefault(x => x.RepairOrderId == b.Id)?.Passed == true,
                photos.FirstOrDefault(x => x.OrderId == b.Id)?.Count ?? 0);
        }

        // Orders whose customer/device were somehow missing still get an entry (defensive).
        foreach (var id in ids.Where(id => !result.ContainsKey(id)))
            result[id] = new OrderReadInfo(id, "—", "", null, false, "—", "", null, null, null, null, 0, null, 0, false, false, false, 0);

        return result;
    }
}
