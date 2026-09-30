using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderRepository : IRepairOrderRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrder?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.RepairOrders.FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, ct);

    public Task<List<RepairOrder>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
        => _db.RepairOrders
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<(List<RepairOrder> Items, int Total)> SearchAsync(Guid shopId, RepairOrderSearchOptions options, CancellationToken ct)
    {
        var q = _db.RepairOrders.AsQueryable().Where(x => x.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(options.Q))
        {
            var term = options.Q.Trim();
            q = q.Where(x => x.IssueDescription.Contains(term) || (x.Notes != null && x.Notes.Contains(term)));
        }

        if (options.Status is not null)
        {
            q = q.Where(x => x.Status == options.Status);
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

    private static IQueryable<RepairOrder> ApplySort(IQueryable<RepairOrder> q, string? sortBy, string? sortDir)
    {
        sortBy = (sortBy ?? "createdAt").Trim();
        sortDir = (sortDir ?? "desc").Trim();
        var desc = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy.ToLowerInvariant(), desc) switch
        {
            ("createdat", true) => q.OrderByDescending(x => x.CreatedAtUtc),
            ("createdat", false) => q.OrderBy(x => x.CreatedAtUtc),

            ("updatedat", true) => q.OrderByDescending(x => x.UpdatedAtUtc),
            ("updatedat", false) => q.OrderBy(x => x.UpdatedAtUtc),

            ("status", true) => q.OrderByDescending(x => x.Status),
            ("status", false) => q.OrderBy(x => x.Status),

            _ => q.OrderByDescending(x => x.CreatedAtUtc)
        };
    }

    public Task AddAsync(RepairOrder order, CancellationToken ct)
        => _db.RepairOrders.AddAsync(order, ct).AsTask();

    public Task RemoveAsync(RepairOrder order, CancellationToken ct)
    {
        _db.RepairOrders.Remove(order);
        return Task.CompletedTask;
    }
}
