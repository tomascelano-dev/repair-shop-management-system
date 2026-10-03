using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Inventory;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class InventoryItemRepository : IInventoryItemRepository
{
    private readonly RepairShopDbContext _db;
    public InventoryItemRepository(RepairShopDbContext db) => _db = db;

    public Task<InventoryItem?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.InventoryItems.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<List<InventoryItem>> GetByIdsAsync(Guid shopId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => _db.InventoryItems.Where(x => x.ShopId == shopId && ids.Contains(x.Id)).ToListAsync(ct);

    public Task<InventoryItem?> GetBySkuAsync(Guid shopId, string sku, CancellationToken ct)
    {
        sku = (sku ?? "").Trim().ToUpperInvariant();
        return _db.InventoryItems.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Sku == sku, ct);
    }

    public Task<InventoryItem?> GetByBarcodeAsync(Guid shopId, string barcode, CancellationToken ct)
    {
        barcode = (barcode ?? "").Trim();
        return _db.InventoryItems.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Barcode == barcode, ct);
    }

    public Task<List<InventoryItem>> ListAsync(Guid shopId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        var q = _db.InventoryItems.Where(x => x.ShopId == shopId);
        if (!includeInactive) q = q.Where(x => x.IsActive);
        return q.OrderBy(x => x.Name).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<(List<InventoryItem> Items, int Total)> SearchAsync(Guid shopId, InventorySearchOptions options, CancellationToken ct)
    {
        var q = _db.InventoryItems.AsQueryable().Where(x => x.ShopId == shopId);

        if (!options.IncludeInactive) q = q.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(options.Q))
        {
            var term = options.Q.Trim();
            var p = Like.Contains(term);
            q = q.Where(x => EF.Functions.ILike(x.Name, p)
                             || EF.Functions.ILike(x.Sku, p)
                             || x.Barcode == term
                             || (x.Category != null && EF.Functions.ILike(x.Category, p)));
        }

        if (!string.IsNullOrWhiteSpace(options.Category))
            q = q.Where(x => x.Category != null && EF.Functions.ILike(x.Category, options.Category.Trim()));

        if (options.OnlySellable == true) q = q.Where(x => x.IsSellable);

        if (options.OnlyLowStock == true)
        {
            q = q.Where(x => x.TrackStock && x.QuantityOnHand - _db.InventoryReservations
                .Where(r => r.InventoryItemId == x.Id && r.Status == ReservationStatus.Active)
                .Sum(r => (int?)(r.Quantity - r.ConsumedQuantity) ?? 0) <= x.MinStock);
        }

        if (!string.IsNullOrWhiteSpace(options.CompatibleBrand) && !string.IsNullOrWhiteSpace(options.CompatibleModel))
        {
            var brand = options.CompatibleBrand.Trim().ToLowerInvariant();
            var model = options.CompatibleModel.Trim().ToLowerInvariant();
            q = q.Where(x => _db.InventoryItemCompatibilities.Any(c => c.InventoryItemId == x.Id && c.BrandKey == brand && c.ModelKey == model));
        }

        if (options.DateFromUtc is not null) q = q.Where(x => x.CreatedAtUtc >= options.DateFromUtc);
        if (options.DateToUtc is not null) q = q.Where(x => x.CreatedAtUtc <= options.DateToUtc);

        q = ApplySort(q, options.SortBy, options.SortDir);

        var total = await q.CountAsync(ct);
        var take = Math.Clamp(options.Take, 1, 200);
        var skip = Math.Max(0, options.Skip);
        var items = await q.Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    private static IQueryable<InventoryItem> ApplySort(IQueryable<InventoryItem> q, string? sortBy, string? sortDir)
    {
        sortBy = (sortBy ?? "name").Trim();
        sortDir = (sortDir ?? "asc").Trim();
        var desc = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy.ToLowerInvariant(), desc) switch
        {
            ("name", true) => q.OrderByDescending(x => x.Name),
            ("name", false) => q.OrderBy(x => x.Name),
            ("sku", true) => q.OrderByDescending(x => x.Sku),
            ("sku", false) => q.OrderBy(x => x.Sku),
            ("stock", true) => q.OrderByDescending(x => x.QuantityOnHand),
            ("stock", false) => q.OrderBy(x => x.QuantityOnHand),
            ("updatedat", true) => q.OrderByDescending(x => x.UpdatedAtUtc),
            ("updatedat", false) => q.OrderBy(x => x.UpdatedAtUtc),
            _ => q.OrderBy(x => x.Name)
        };
    }

    public Task AddAsync(InventoryItem item, CancellationToken ct)
        => _db.InventoryItems.AddAsync(item, ct).AsTask();

    public Task RemoveAsync(InventoryItem item, CancellationToken ct)
    {
        _db.InventoryItems.Remove(item);
        return Task.CompletedTask;
    }
}

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

public sealed class InventoryReservationRepository : IInventoryReservationRepository
{
    private readonly RepairShopDbContext _db;
    public InventoryReservationRepository(RepairShopDbContext db) => _db = db;

    public Task<List<InventoryReservation>> ListActiveByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.InventoryReservations
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId && x.Status == ReservationStatus.Active)
            .ToListAsync(ct);

    public async Task<Dictionary<Guid, int>> SumActiveByItemsAsync(Guid shopId, IReadOnlyCollection<Guid> itemIds, Guid? excludeOrderId, CancellationToken ct)
    {
        var rows = await _db.InventoryReservations
            .Where(x => x.ShopId == shopId && itemIds.Contains(x.InventoryItemId) && x.Status == ReservationStatus.Active
                        && (excludeOrderId == null || x.RepairOrderId != excludeOrderId))
            .GroupBy(x => x.InventoryItemId)
            .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity - x.ConsumedQuantity) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.ItemId, r => r.Qty);
    }

    public Task AddAsync(InventoryReservation reservation, CancellationToken ct)
        => _db.InventoryReservations.AddAsync(reservation, ct).AsTask();
}

public sealed class InventoryCompatibilityRepository : IInventoryCompatibilityRepository
{
    private readonly RepairShopDbContext _db;
    public InventoryCompatibilityRepository(RepairShopDbContext db) => _db = db;

    public Task<List<InventoryItemCompatibility>> ListByItemAsync(Guid shopId, Guid itemId, CancellationToken ct)
        => _db.InventoryItemCompatibilities.Where(x => x.ShopId == shopId && x.InventoryItemId == itemId)
            .OrderBy(x => x.Brand).ThenBy(x => x.Model).ToListAsync(ct);

    public Task<List<Guid>> FindItemIdsAsync(Guid shopId, string brand, string model, CancellationToken ct)
    {
        brand = (brand ?? "").Trim().ToLowerInvariant();
        model = (model ?? "").Trim().ToLowerInvariant();
        return _db.InventoryItemCompatibilities
            .Where(x => x.ShopId == shopId && x.BrandKey == brand && x.ModelKey == model)
            .Select(x => x.InventoryItemId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task AddAsync(InventoryItemCompatibility compatibility, CancellationToken ct)
        => _db.InventoryItemCompatibilities.AddAsync(compatibility, ct).AsTask();

    public void Remove(InventoryItemCompatibility compatibility) => _db.InventoryItemCompatibilities.Remove(compatibility);
}

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly RepairShopDbContext _db;
    public SupplierRepository(RepairShopDbContext db) => _db = db;

    public Task<Supplier?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.Suppliers.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public async Task<(List<Supplier> Items, int Total)> SearchAsync(Guid shopId, string? q, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        var query = _db.Suppliers.Where(x => x.ShopId == shopId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var p = Like.Contains(q);
            query = query.Where(x => EF.Functions.ILike(x.Name, p)
                                     || (x.ContactName != null && EF.Functions.ILike(x.ContactName, p))
                                     || (x.Phone != null && EF.Functions.ILike(x.Phone, p))
                                     || (x.Email != null && EF.Functions.ILike(x.Email, p)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(Supplier supplier, CancellationToken ct)
        => _db.Suppliers.AddAsync(supplier, ct).AsTask();
}

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly RepairShopDbContext _db;
    public PurchaseOrderRepository(RepairShopDbContext db) => _db = db;

    public Task<PurchaseOrder?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.PurchaseOrders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public async Task<(List<PurchaseOrder> Items, int Total)> SearchAsync(Guid shopId, PurchaseOrderStatus? status, Guid? supplierId, int skip, int take, CancellationToken ct)
    {
        var q = _db.PurchaseOrders.Include(x => x.Lines).Where(x => x.ShopId == shopId);
        if (status is not null) q = q.Where(x => x.Status == status);
        if (supplierId is not null) q = q.Where(x => x.SupplierId == supplierId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.Number).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(PurchaseOrder po, CancellationToken ct)
        => _db.PurchaseOrders.AddAsync(po, ct).AsTask();
}

public sealed class StockTransferRepository : IStockTransferRepository
{
    private readonly RepairShopDbContext _db;
    public StockTransferRepository(RepairShopDbContext db) => _db = db;

    public Task<StockTransfer?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.StockTransfers.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(List<StockTransfer> Items, int Total)> SearchForShopAsync(Guid shopId, StockTransferStatus? status, int skip, int take, CancellationToken ct)
    {
        var q = _db.StockTransfers.Include(x => x.Lines).Where(x => x.FromShopId == shopId || x.ToShopId == shopId);
        if (status is not null) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(StockTransfer transfer, CancellationToken ct)
        => _db.StockTransfers.AddAsync(transfer, ct).AsTask();
}
