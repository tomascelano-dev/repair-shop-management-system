using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly RepairShopDbContext _db;

    public UnitOfWork(RepairShopDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
