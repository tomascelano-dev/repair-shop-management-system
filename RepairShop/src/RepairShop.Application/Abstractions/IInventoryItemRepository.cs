using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Abstractions;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<InventoryItem?> GetBySkuAsync(Guid shopId, string sku, CancellationToken ct);
    Task<List<InventoryItem>> ListAsync(Guid shopId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task<(List<InventoryItem> Items, int Total)> SearchAsync(Guid shopId, InventorySearchOptions options, CancellationToken ct);
    Task AddAsync(InventoryItem item, CancellationToken ct);
    Task RemoveAsync(InventoryItem item, CancellationToken ct);
}

public sealed record InventorySearchOptions(
    string? Q = null,
    bool IncludeInactive = false,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    string? SortBy = null,
    string? SortDir = null,
    int Skip = 0,
    int Take = 50);
