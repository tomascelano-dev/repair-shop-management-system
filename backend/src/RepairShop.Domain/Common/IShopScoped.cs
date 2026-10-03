namespace RepairShop.Domain.Common;

/// <summary>
/// Marker for entities that belong to a single shop (tenant).
/// Infrastructure applies a global query filter on these as defense in depth.
/// </summary>
public interface IShopScoped
{
    Guid ShopId { get; }
}
