using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Abstractions;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<InventoryItem>> GetByIdsAsync(Guid shopId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<InventoryItem?> GetBySkuAsync(Guid shopId, string sku, CancellationToken ct);
    Task<InventoryItem?> GetByBarcodeAsync(Guid shopId, string barcode, CancellationToken ct);
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
    int Take = 50,
    bool? OnlySellable = null,
    bool? OnlyLowStock = null,
    string? Category = null,
    string? CompatibleBrand = null,
    string? CompatibleModel = null);

public interface IInventoryReservationRepository
{
    Task<List<InventoryReservation>> ListActiveByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task<Dictionary<Guid, int>> SumActiveByItemsAsync(Guid shopId, IReadOnlyCollection<Guid> itemIds, Guid? excludeOrderId, CancellationToken ct);
    Task AddAsync(InventoryReservation reservation, CancellationToken ct);
}

public interface IInventoryCompatibilityRepository
{
    Task<List<InventoryItemCompatibility>> ListByItemAsync(Guid shopId, Guid itemId, CancellationToken ct);
    Task<List<Guid>> FindItemIdsAsync(Guid shopId, string brand, string model, CancellationToken ct);
    Task AddAsync(InventoryItemCompatibility compatibility, CancellationToken ct);
    void Remove(InventoryItemCompatibility compatibility);
}

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<(List<Supplier> Items, int Total)> SearchAsync(Guid shopId, string? q, bool includeInactive, int skip, int take, CancellationToken ct);
    Task AddAsync(Supplier supplier, CancellationToken ct);
}

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<(List<PurchaseOrder> Items, int Total)> SearchAsync(Guid shopId, PurchaseOrderStatus? status, Guid? supplierId, int skip, int take, CancellationToken ct);
    Task AddAsync(PurchaseOrder po, CancellationToken ct);
}

public interface IStockTransferRepository
{
    Task<StockTransfer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(List<StockTransfer> Items, int Total)> SearchForShopAsync(Guid shopId, StockTransferStatus? status, int skip, int take, CancellationToken ct);
    Task AddAsync(StockTransfer transfer, CancellationToken ct);
}
