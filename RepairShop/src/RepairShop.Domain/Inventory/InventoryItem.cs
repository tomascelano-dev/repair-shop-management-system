using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class InventoryItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int QuantityOnHand { get; private set; }

    public decimal? UnitCost { get; private set; }
    public string? UnitCostCurrency { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private InventoryItem() { }

    public InventoryItem(Guid shopId, string sku, string name, int initialQty, decimal? unitCost, string? unitCostCurrency, bool isActive, DateTime nowUtc)
    {
        ShopId = shopId;
        Sku = NormalizeSku(sku);
        Name = (name ?? "").Trim();
        QuantityOnHand = initialQty;
        UnitCost = unitCost is null ? null : decimal.Round(unitCost.Value, 2, MidpointRounding.AwayFromZero);
        UnitCostCurrency = unitCostCurrency?.Trim().ToUpperInvariant();
        IsActive = isActive;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (Name.Length < 2) throw new DomainException("Inventory item name is required.");
        if (QuantityOnHand < 0) throw new DomainException("Inventory quantity cannot be negative.");
        if (UnitCost is < 0) throw new DomainException("Unit cost cannot be negative.");
    }

    public void Update(string name, bool isActive, DateTime nowUtc)
    {
        Name = (name ?? "").Trim();
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
        if (Name.Length < 2) throw new DomainException("Inventory item name is required.");
    }

    public void ApplyDelta(int deltaQty, DateTime nowUtc)
    {
        var next = QuantityOnHand + deltaQty;
        if (next < 0) throw new DomainException("Inventory cannot go below 0.");
        QuantityOnHand = next;
        UpdatedAtUtc = nowUtc;
    }

    public void SetCost(decimal amount, string currency, DateTime nowUtc)
    {
        if (amount < 0) throw new DomainException("El costo no puede ser negativo.");
        UnitCost = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        UnitCostCurrency = currency;
        UpdatedAtUtc = nowUtc;
    }

    private static string NormalizeSku(string sku)
    {
        sku = (sku ?? "").Trim().ToUpperInvariant();
        if (sku.Length < 2) throw new DomainException("Inventory SKU is required.");
        return sku;
    }
}
