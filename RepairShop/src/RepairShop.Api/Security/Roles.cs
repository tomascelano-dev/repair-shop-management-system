namespace RepairShop.Api.Security;

/// <summary>
/// Centralized role names used across auth policies and docs.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";

    // Primary technician/operator role used by the domain enum (UserRole.Tech).
    public const string Tech = "Tech";

    // Alias used in docs in older versions (kept for compatibility).
    public const string Staff = "Staff";
}
