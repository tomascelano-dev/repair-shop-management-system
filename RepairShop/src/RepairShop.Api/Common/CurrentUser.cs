using System.Security.Claims;

namespace RepairShop.Api.Common;

public static class CurrentUser
{
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? user.FindFirstValue("uid");

        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static Guid GetShopId(ClaimsPrincipal user)
    {
        var sid = user.FindFirstValue("shop_id")
               ?? user.FindFirstValue("sid")
               ?? user.FindFirstValue("shopId");

        return Guid.TryParse(sid, out var id) ? id : Guid.Empty;
    }

    public static string? GetEmail(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
}
