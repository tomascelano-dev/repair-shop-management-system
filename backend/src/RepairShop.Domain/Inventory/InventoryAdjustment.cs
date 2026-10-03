using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class InventoryAdjustment : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public Guid InventoryItemId { get; private set; }
    public InventoryAdjustmentType Type { get; private set; }

    public int DeltaQuantity { get; private set; }
    public string? Reason { get; private set; }

    public Guid? RepairOrderId { get; private set; }

    // Generic reference to the document that moved stock (sale, purchase order, transfer...).
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }

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
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 300)];
        RepairOrderId = repairOrderId;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (InventoryItemId == Guid.Empty) throw new DomainException("El movimiento debe referenciar un ítem.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("El movimiento debe tener un usuario.");
        if (DeltaQuantity == 0) throw new DomainException("La cantidad del movimiento no puede ser 0.");
        if (!Enum.IsDefined(type)) throw new DomainException("Tipo de movimiento inválido.");
    }

    public InventoryAdjustment WithReference(string referenceType, Guid referenceId)
    {
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        return this;
    }
}
