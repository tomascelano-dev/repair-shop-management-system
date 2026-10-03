using RepairShop.Domain.Users;

namespace RepairShop.Application.Security;

/// <summary>
/// Coarse permissions derived from the role. The API enforces them with policies;
/// the frontend uses them to show/hide features.
/// </summary>
public static class Permissions
{
    public const string OrdersManage = "orders.manage";       // create/edit orders, customers, devices
    public const string OrdersWork = "orders.work";           // status, quotes, QA, notes, parts
    public const string Sales = "sales";                       // POS, payments, cash register
    public const string InventoryManage = "inventory.manage"; // items, purchases, suppliers, transfers
    public const string Reports = "reports";
    public const string Admin = "admin";                       // users, settings, templates, integrations

    public static IReadOnlyList<string> For(UserRole role) => role switch
    {
        UserRole.Admin => new[] { OrdersManage, OrdersWork, Sales, InventoryManage, Reports, Admin },
        UserRole.Tech => new[] { OrdersManage, OrdersWork },
        UserRole.Reception => new[] { OrdersManage, OrdersWork, Sales },
        UserRole.Cashier => new[] { Sales },
        _ => Array.Empty<string>()
    };
}
