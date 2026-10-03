using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

/// <summary>
/// "This part fits these device models" (e.g. Módulo iPhone 11 -> Apple / iPhone 11).
/// </summary>
public sealed class InventoryItemCompatibility : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public string Brand { get; private set; } = null!;
    public string Model { get; private set; } = null!;

    // Lower-case copies for case-insensitive matching.
    public string BrandKey { get; private set; } = null!;
    public string ModelKey { get; private set; } = null!;

    private InventoryItemCompatibility() { }

    public InventoryItemCompatibility(Guid shopId, Guid inventoryItemId, string brand, string model)
    {
        brand = (brand ?? "").Trim();
        model = (model ?? "").Trim();
        if (inventoryItemId == Guid.Empty) throw new DomainException("La compatibilidad debe referenciar un ítem.");
        if (brand.Length < 2 || brand.Length > 60) throw new DomainException("Marca inválida.");
        if (model.Length < 1 || model.Length > 60) throw new DomainException("Modelo inválido.");

        ShopId = shopId;
        InventoryItemId = inventoryItemId;
        Brand = brand;
        Model = model;
        BrandKey = brand.ToLowerInvariant();
        ModelKey = model.ToLowerInvariant();
    }
}
