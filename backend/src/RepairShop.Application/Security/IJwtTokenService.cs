using RepairShop.Domain.Users;

namespace RepairShop.Application.Security;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    /// <summary>Back-compat: token for the user's home shop and role.</summary>
    string CreateToken(AppUser user);

    AccessToken CreateAccessToken(AppUser user, Guid shopId, UserRole role, Guid organizationId);

    TimeSpan RefreshTokenLifetime { get; }
}
