using System.Security.Claims;
using RepairShop.Application.Common;
using RepairShop.Domain.Users;

namespace RepairShop.Api.Common;

public static class ClaimNames
{
    public const string UserId = "sub";
    public const string ShopId = "shop_id";
    public const string OrganizationId = "org_id";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string SecurityStamp = "sstamp";
}

public static class CurrentUser
{
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimNames.UserId) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static Guid GetShopId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimNames.ShopId), out var id) ? id : Guid.Empty;

    public static Guid GetOrganizationId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimNames.OrganizationId), out var id) ? id : Guid.Empty;

    public static string? GetEmail(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);

    public static string? GetRoleName(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimNames.Role) ?? user.FindFirstValue(ClaimTypes.Role);

    public static UserRole? GetRole(ClaimsPrincipal user)
        => Enum.TryParse<UserRole>(GetRoleName(user), ignoreCase: true, out var role) ? role : null;

    public static Actor GetActor(ClaimsPrincipal user) => new(GetUserId(user), GetEmail(user), GetRoleName(user));
}
