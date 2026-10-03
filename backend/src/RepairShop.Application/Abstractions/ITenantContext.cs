namespace RepairShop.Application.Abstractions;

/// <summary>
/// Shop (tenant) of the current request. Null for system work (background jobs, public endpoints):
/// in that case the code must filter by shop explicitly.
/// </summary>
public interface ITenantContext
{
    Guid? ShopId { get; }
}

public sealed class NoTenantContext : ITenantContext
{
    public static readonly NoTenantContext Instance = new();
    public Guid? ShopId => null;
}
