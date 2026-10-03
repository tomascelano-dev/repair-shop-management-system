using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RepairShop.Api.Common;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Security;

/// <summary>
/// Rejects access tokens issued before a password change, "close all sessions", deactivation or a role
/// change (the user's security stamp rotates). Cached briefly; a mismatch always re-checks the database.
/// </summary>
public static class SecurityStampValidator
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private static string Key(Guid userId) => $"user-stamp:{userId:N}";

    /// <summary>Forget the cached stamp after a local change (logout-all, password, role, access).</summary>
    public static void Evict(IMemoryCache cache, Guid userId) => cache.Remove(Key(userId));

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Token inválido.");
            return;
        }

        var userId = CurrentUser.GetUserId(principal);
        var stamp = principal.FindFirst(ClaimNames.SecurityStamp)?.Value;
        if (userId == Guid.Empty || string.IsNullOrEmpty(stamp))
        {
            context.Fail("Token inválido.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();
        var key = Key(userId);

        if (cache.TryGetValue<UserStamp>(key, out var cached) && cached is not null && cached.IsActive && cached.Stamp == stamp)
            return;

        var db = services.GetRequiredService<RepairShopDbContext>();
        var current = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserStamp(u.SecurityStamp, u.IsActive))
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (current is null || !current.IsActive || current.Stamp != stamp)
        {
            if (current is not null) cache.Set(key, current, CacheFor);
            context.Fail("La sesión ya no es válida.");
            return;
        }

        cache.Set(key, current, CacheFor);
    }

    private sealed record UserStamp(string Stamp, bool IsActive);
}
