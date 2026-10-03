using Microsoft.AspNetCore.Authorization;
using RepairShop.Api.Common;
using RepairShop.Application.Security;

namespace RepairShop.Api.Security;

/// <summary>Policy names. Permissions are derived from the role in the token (see Application.Security.Permissions).</summary>
public static class Policies
{
    /// <summary>Any authenticated staff member (read access to the shop).</summary>
    public const string StaffOnly = "StaffOnly";
    public const string AdminOnly = "AdminOnly";
    public const string OrdersManage = "OrdersManage";
    public const string OrdersWork = "OrdersWork";
    public const string Sales = "Sales";
    public const string InventoryManage = "InventoryManage";
    public const string Reports = "Reports";

    public const string CorsDefault = "CorsDefault";

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(StaffOnly, p => p.RequireAuthenticatedUser().RequireClaim(ClaimNames.ShopId));
        options.AddPolicy(AdminOnly, p => RequirePermission(p, Permissions.Admin));
        options.AddPolicy(OrdersManage, p => RequirePermission(p, Permissions.OrdersManage));
        options.AddPolicy(OrdersWork, p => RequirePermission(p, Permissions.OrdersWork));
        options.AddPolicy(Sales, p => RequirePermission(p, Permissions.Sales));
        options.AddPolicy(InventoryManage, p => RequirePermission(p, Permissions.InventoryManage));
        options.AddPolicy(Reports, p => RequirePermission(p, Permissions.Reports));

        options.DefaultPolicy = options.GetPolicy(StaffOnly)!;
        options.FallbackPolicy = options.GetPolicy(StaffOnly)!;
    }

    private static void RequirePermission(AuthorizationPolicyBuilder p, string permission)
        => p.RequireAuthenticatedUser()
            .RequireClaim(ClaimNames.ShopId)
            .RequireAssertion(ctx => CurrentUser.GetRole(ctx.User) is { } role && Permissions.For(role).Contains(permission));
}
