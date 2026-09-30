using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class RepairOrderPartUsage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public int QuantityUsed { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public string? UnitPriceCurrency { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RepairOrderPartUsage() { }

    public RepairOrderPartUsage(
        Guid shopId,
        Guid repairOrderId,
        Guid inventoryItemId,
        int quantityUsed,
        decimal? unitPrice,
        string? unitPriceCurrency,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        InventoryItemId = inventoryItemId;
        QuantityUsed = quantityUsed;
        UnitPrice = unitPrice is null ? null : decimal.Round(unitPrice.Value, 2, MidpointRounding.AwayFromZero);
        UnitPriceCurrency = unitPriceCurrency?.Trim().ToUpperInvariant();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("Part usage must belong to an order.");
        if (InventoryItemId == Guid.Empty) throw new DomainException("Part usage must reference an inventory item.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("Part usage must have an author user id.");
        if (QuantityUsed <= 0) throw new DomainException("Quantity used must be > 0.");
        if (UnitPrice is < 0) throw new DomainException("Unit price cannot be negative.");
    }
}
