using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Abstractions;

public interface IInventoryAdjustmentRepository
{
    Task AddAsync(InventoryAdjustment adjustment, CancellationToken ct);
    Task<List<InventoryAdjustment>> ListByItemAsync(Guid shopId, Guid inventoryItemId, int skip, int take, CancellationToken ct);
}
