using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly RepairShopDbContext _db;
    public UserRepository(RepairShopDbContext db) => _db = db;

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task AddAsync(AppUser user, CancellationToken ct)
        => _db.Users.AddAsync(user, ct).AsTask();

    public Task<bool> AnyAsync(CancellationToken ct)
        => _db.Users.AnyAsync(ct);
}
