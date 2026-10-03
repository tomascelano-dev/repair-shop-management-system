using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class ShopRepository : IShopRepository
{
    private readonly RepairShopDbContext _db;
    public ShopRepository(RepairShopDbContext db) => _db = db;

    public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Shops.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<Shop>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => _db.Shops.Where(x => ids.Contains(x.Id)).ToListAsync(ct);

    public Task<List<Shop>> ListAsync(int skip, int take, CancellationToken ct)
        => _db.Shops.OrderBy(x => x.Name).Skip(skip).Take(take).ToListAsync(ct);

    public Task<List<Shop>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct)
        => _db.Shops.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);

    public Task AddAsync(Shop shop, CancellationToken ct)
        => _db.Shops.AddAsync(shop, ct).AsTask();
}

public sealed class ShopIntegrationRepository : IShopIntegrationRepository
{
    private readonly RepairShopDbContext _db;
    public ShopIntegrationRepository(RepairShopDbContext db) => _db = db;

    public Task<ShopIntegration?> GetAsync(Guid shopId, CancellationToken ct)
        => _db.ShopIntegrations.FirstOrDefaultAsync(x => x.ShopId == shopId, ct);

    public Task AddAsync(ShopIntegration integration, CancellationToken ct)
        => _db.ShopIntegrations.AddAsync(integration, ct).AsTask();
}

public sealed class UserRepository : IUserRepository
{
    private readonly RepairShopDbContext _db;
    public UserRepository(RepairShopDbContext db) => _db = db;

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct)
    {
        email = AppUser.NormalizeEmail(email);
        return _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
    }

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<AppUser?> GetByPendingTokenHashAsync(string tokenHash, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.PendingTokenHash == tokenHash, ct);

    public Task<List<AppUser>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => _db.Users.Where(x => ids.Contains(x.Id)).ToListAsync(ct);

    public Task<List<AppUser>> ListForShopAsync(Guid shopId, CancellationToken ct)
        => _db.Users
            .Where(u => u.ShopId == shopId || _db.UserShopAccess.Any(a => a.UserId == u.Id && a.ShopId == shopId))
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

    public Task<int> CountActiveAdminsAsync(Guid shopId, CancellationToken ct)
        => _db.Users.CountAsync(u => u.IsActive && (
            (u.ShopId == shopId && u.Role == UserRole.Admin)
            || _db.UserShopAccess.Any(a => a.UserId == u.Id && a.ShopId == shopId && a.Role == UserRole.Admin)), ct);

    public Task AddAsync(AppUser user, CancellationToken ct)
        => _db.Users.AddAsync(user, ct).AsTask();

    public Task<bool> AnyAsync(CancellationToken ct)
        => _db.Users.AnyAsync(ct);
}

public sealed class UserShopAccessRepository : IUserShopAccessRepository
{
    private readonly RepairShopDbContext _db;
    public UserShopAccessRepository(RepairShopDbContext db) => _db = db;

    public Task<List<UserShopAccess>> ListByUserAsync(Guid userId, CancellationToken ct)
        => _db.UserShopAccess.Where(x => x.UserId == userId).ToListAsync(ct);

    public Task<List<UserShopAccess>> ListByShopAsync(Guid shopId, CancellationToken ct)
        => _db.UserShopAccess.Where(x => x.ShopId == shopId).ToListAsync(ct);

    public Task<UserShopAccess?> GetAsync(Guid userId, Guid shopId, CancellationToken ct)
        => _db.UserShopAccess.FirstOrDefaultAsync(x => x.UserId == userId && x.ShopId == shopId, ct);

    public Task AddAsync(UserShopAccess access, CancellationToken ct)
        => _db.UserShopAccess.AddAsync(access, ct).AsTask();

    public void Remove(UserShopAccess access) => _db.UserShopAccess.Remove(access);
}

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly RepairShopDbContext _db;
    public RefreshTokenRepository(RepairShopDbContext db) => _db = db;

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct)
        => _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public Task<List<RefreshToken>> ListActiveByUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
        => _db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > nowUtc).ToListAsync(ct);

    public Task<List<RefreshToken>> ListByFamilyAsync(Guid userId, Guid familyId, CancellationToken ct)
        => _db.RefreshTokens.Where(x => x.UserId == userId && x.FamilyId == familyId).ToListAsync(ct);

    public Task AddAsync(RefreshToken token, CancellationToken ct)
        => _db.RefreshTokens.AddAsync(token, ct).AsTask();

    public Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken ct)
        => _db.RefreshTokens.Where(x => x.ExpiresAtUtc < olderThanUtc).ExecuteDeleteAsync(ct);
}
