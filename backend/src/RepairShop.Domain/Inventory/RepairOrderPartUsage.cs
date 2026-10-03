using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class RepairOrderPartUsage : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public Guid RepairOrderId { get; private set; }
    public Guid InventoryItemId { get; private set; }

    public int QuantityUsed { get; private set; }

    // Price charged to the customer for this part (only when ChargedToCustomer).
    public decimal? UnitPrice { get; private set; }
    public string? UnitPriceCurrency { get; private set; }

    // Extra charge on top of the agreed price. Parts already included in the approved quote are not charged twice.
    public bool ChargedToCustomer { get; private set; }

    // Cost snapshot at the time of use (for margins).
    public decimal? UnitCost { get; private set; }
    public string? UnitCostCurrency { get; private set; }

    public Guid? ReservationId { get; private set; }

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
        UnitPrice = unitPrice is null ? null : Money.Round(unitPrice.Value);
        UnitPriceCurrency = unitPrice is null || string.IsNullOrWhiteSpace(unitPriceCurrency) ? null : Money.NormalizeCurrency(unitPriceCurrency);
        ChargedToCustomer = unitPrice is > 0;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("El repuesto debe pertenecer a una orden.");
        if (InventoryItemId == Guid.Empty) throw new DomainException("Falta el ítem de inventario.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("El uso de repuesto debe tener un usuario.");
        if (QuantityUsed <= 0) throw new DomainException("La cantidad usada debe ser mayor a 0.");
        if (UnitPrice is < 0) throw new DomainException("El precio no puede ser negativo.");
        if (ChargedToCustomer && UnitPriceCurrency is null) throw new DomainException("Indicá la moneda del precio del repuesto.");
    }

    public void SnapshotCost(decimal? unitCost, string? currency)
    {
        UnitCost = unitCost;
        UnitCostCurrency = unitCost is null ? null : currency;
    }

    public void LinkReservation(Guid reservationId) => ReservationId = reservationId;

    public decimal ExtraCharge => ChargedToCustomer && UnitPrice is not null ? Money.Round(UnitPrice.Value * QuantityUsed) : 0m;
}
