using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Shops;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class ShopRepository : IShopRepository
{
    private readonly RepairShopDbContext _db;
    public ShopRepository(RepairShopDbContext db) => _db = db;

    public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Shops.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<Shop>> ListAsync(int skip, int take, CancellationToken ct)
        => _db.Shops.OrderBy(x => x.Name).Skip(skip).Take(take).ToListAsync(ct);

    public Task AddAsync(Shop shop, CancellationToken ct)
        => _db.Shops.AddAsync(shop, ct).AsTask();
}
