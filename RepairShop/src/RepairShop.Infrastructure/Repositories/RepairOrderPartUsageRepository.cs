using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Inventory;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderPartUsageRepository : IRepairOrderPartUsageRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderPartUsageRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(RepairOrderPartUsage usage, CancellationToken ct)
        => _db.RepairOrderPartUsages.AddAsync(usage, ct).AsTask();

    public Task<List<RepairOrderPartUsage>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderPartUsages
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
}
