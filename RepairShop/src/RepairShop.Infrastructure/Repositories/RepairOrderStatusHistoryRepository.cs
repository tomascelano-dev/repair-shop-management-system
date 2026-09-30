using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderStatusHistoryRepository : IRepairOrderStatusHistoryRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderStatusHistoryRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(RepairOrderStatusHistory history, CancellationToken ct)
        => _db.RepairOrderStatusHistory.AddAsync(history, ct).AsTask();

    public Task<List<RepairOrderStatusHistory>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderStatusHistory
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .ToListAsync(ct);
}
