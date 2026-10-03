using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Customers;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Queries;

public sealed class CustomerMergeStore : ICustomerMergeStore
{
    private readonly RepairShopDbContext _db;
    public CustomerMergeStore(RepairShopDbContext db) => _db = db;

    public async Task<int> ReassignAsync(Guid shopId, Guid fromCustomerId, Guid toCustomerId, CancellationToken ct)
    {
        var moved = 0;
        moved += await _db.Devices.Where(x => x.ShopId == shopId && x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), ct);
        moved += await _db.RepairOrders.Where(x => x.ShopId == shopId && x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), ct);
        moved += await _db.Sales.Where(x => x.ShopId == shopId && x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), ct);
        moved += await _db.CustomerFeedback.Where(x => x.ShopId == shopId && x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), ct);
        return moved;
    }

    public async Task<double?> AverageFeedbackAsync(Guid shopId, Guid customerId, CancellationToken ct)
        => await _db.CustomerFeedback.Where(f => f.ShopId == shopId && f.CustomerId == customerId).AverageAsync(f => (double?)f.Score, ct);
}
