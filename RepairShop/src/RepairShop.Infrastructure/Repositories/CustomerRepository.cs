using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Customers;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly RepairShopDbContext _db;
    public CustomerRepository(RepairShopDbContext db) => _db = db;

    public Task<Customer?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.Customers.FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, ct);

    public Task<List<Customer>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
        => _db.Customers
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<(List<Customer> Items, int Total)> SearchAsync(Guid shopId, CustomerSearchOptions options, CancellationToken ct)
    {
        var q = _db.Customers.AsQueryable().Where(x => x.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(options.Q))
        {
            var term = options.Q.Trim();
            q = q.Where(x => x.FullName.Contains(term) || x.Phone.Contains(term) || (x.Notes != null && x.Notes.Contains(term)));
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

    private static IQueryable<Customer> ApplySort(IQueryable<Customer> q, string? sortBy, string? sortDir)
    {
        sortBy = (sortBy ?? "createdAt").Trim();
        sortDir = (sortDir ?? "desc").Trim();
        var desc = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy.ToLowerInvariant(), desc) switch
        {
            ("createdat", true) => q.OrderByDescending(x => x.CreatedAtUtc),
            ("createdat", false) => q.OrderBy(x => x.CreatedAtUtc),
            ("name", true) => q.OrderByDescending(x => x.FullName),
            ("name", false) => q.OrderBy(x => x.FullName),
            _ => q.OrderByDescending(x => x.CreatedAtUtc)
        };
    }

    public Task AddAsync(Customer customer, CancellationToken ct)
        => _db.Customers.AddAsync(customer, ct).AsTask();

    public Task RemoveAsync(Customer customer, CancellationToken ct)
    {
        _db.Customers.Remove(customer);
        return Task.CompletedTask;
    }
}
