using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class InventoryAdjustment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid InventoryItemId { get; private set; }

    public InventoryAdjustmentType Type { get; private set; }
    public int DeltaQuantity { get; private set; }
    public string? Reason { get; private set; }
    public Guid? RepairOrderId { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private InventoryAdjustment() { }

    public InventoryAdjustment(
        Guid shopId,
        Guid inventoryItemId,
        InventoryAdjustmentType type,
        int deltaQuantity,
        string? reason,
        Guid? repairOrderId,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        ShopId = shopId;
        InventoryItemId = inventoryItemId;
        Type = type;
        DeltaQuantity = deltaQuantity;
        Reason = reason?.Trim();
        RepairOrderId = repairOrderId;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (InventoryItemId == Guid.Empty) throw new DomainException("Adjustment must reference an inventory item.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("Adjustment must have an author user id.");
        if (DeltaQuantity == 0) throw new DomainException("Adjustment delta cannot be 0.");
    }
}
