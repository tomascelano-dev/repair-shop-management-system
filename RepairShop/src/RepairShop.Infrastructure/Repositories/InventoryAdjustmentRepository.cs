using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Inventory;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class InventoryAdjustmentRepository : IInventoryAdjustmentRepository
{
    private readonly RepairShopDbContext _db;
    public InventoryAdjustmentRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(InventoryAdjustment adjustment, CancellationToken ct)
        => _db.InventoryAdjustments.AddAsync(adjustment, ct).AsTask();

    public Task<List<InventoryAdjustment>> ListByItemAsync(Guid shopId, Guid inventoryItemId, int skip, int take, CancellationToken ct)
        => _db.InventoryAdjustments
            .Where(x => x.ShopId == shopId && x.InventoryItemId == inventoryItemId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
}
