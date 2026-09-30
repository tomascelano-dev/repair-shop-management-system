namespace RepairShop.Api.Security;

/// <summary>
/// Centralized policy names so controllers don't rely on magic strings.
/// </summary>
public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string StaffOnly = "StaffOnly";
    public const string TechOnly = "TechOnly";

    public const string CorsDefault = "CorsDefault";
}
