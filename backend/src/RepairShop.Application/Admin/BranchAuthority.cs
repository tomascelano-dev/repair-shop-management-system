using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Admin;

/// <summary>
/// Being Admin in one branch says nothing about the others: actions that reach another branch (granting access to it,
/// deactivating it) need the actor to be Admin there too. Actions on the whole organization (plan, billing) need the
/// actor to be Admin of the root branch, whose id is the organization id.
/// </summary>
public static class BranchAuthority
{
    public static async Task<UserRole?> RoleInShopAsync(IUserRepository users, IUserShopAccessRepository access, Guid userId, Guid shopId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return null;
        return user.ShopId == shopId ? user.Role : (await access.GetAsync(user.Id, shopId, ct))?.Role;
    }

    public static async Task RequireAdminInAsync(IUserRepository users, IUserShopAccessRepository access, Guid userId, Guid shopId, CancellationToken ct)
    {
        if (await RoleInShopAsync(users, access, userId, shopId, ct) != UserRole.Admin)
            throw new ForbiddenException("Necesitás ser administrador de esa sucursal.");
    }

    /// <summary>Organization-wide actions: Admin of the root branch (shop id == organization id).</summary>
    public static Task RequireOrganizationAdminAsync(IUserRepository users, IUserShopAccessRepository access, Guid userId, Guid organizationId, CancellationToken ct)
        => RequireAdminInAsync(users, access, userId, organizationId, ct);
}
