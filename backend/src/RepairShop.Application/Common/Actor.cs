namespace RepairShop.Application.Common;

/// <summary>Who performs an operation (for audit and authorization checks inside services).</summary>
public sealed record Actor(Guid UserId, string? Email, string? Role)
{
    public static readonly Actor System = new(Guid.Empty, "system", null);

    public bool IsSystem => UserId == Guid.Empty;
    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}
