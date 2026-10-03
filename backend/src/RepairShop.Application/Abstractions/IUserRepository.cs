using RepairShop.Domain.Users;

namespace RepairShop.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AppUser?> GetByPendingTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<AppUser?> GetByEmailVerificationTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<List<AppUser>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Users whose home shop is the shop or that were granted access to it.</summary>
    Task<List<AppUser>> ListForShopAsync(Guid shopId, CancellationToken ct);

    Task<int> CountActiveAdminsAsync(Guid shopId, CancellationToken ct);
    Task AddAsync(AppUser user, CancellationToken ct);
    Task<bool> AnyAsync(CancellationToken ct);
}

public interface IUserShopAccessRepository
{
    Task<List<UserShopAccess>> ListByUserAsync(Guid userId, CancellationToken ct);
    Task<List<UserShopAccess>> ListByShopAsync(Guid shopId, CancellationToken ct);
    Task<UserShopAccess?> GetAsync(Guid userId, Guid shopId, CancellationToken ct);
    Task AddAsync(UserShopAccess access, CancellationToken ct);
    void Remove(UserShopAccess access);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task<List<RefreshToken>> ListActiveByUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct);
    Task<List<RefreshToken>> ListByFamilyAsync(Guid userId, Guid familyId, CancellationToken ct);
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken ct);
}
