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

    public Task<InventoryItem?> GetBySkuAsync(Guid shopId, string sku, CancellationToken ct)
    {
        sku = (sku ?? "").Trim().ToUpperInvariant();
        return _db.InventoryItems.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Sku == sku, ct);
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
            q = q.Where(x => x.Name.Contains(term) || x.Sku.Contains(term));
        }

        if (options.DateFromUtc is not null)
        {
            q = q.Where(x => x.CreatedAtUtc >= options.DateFromUtc);
        }

        if (options.DateToUtc is not null)
        {
            q = q.Where(x => x.CreatedAtUtc <= options.DateToUtc);
        }

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
