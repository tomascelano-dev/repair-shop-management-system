using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public enum ReservationStatus
{
    Active = 0,
    Consumed = 1,
    Released = 2
}

/// <summary>
/// Stock promised to a repair order when its quote is approved, so it can't be sold or used elsewhere.
/// </summary>
public sealed class InventoryReservation : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public Guid RepairOrderId { get; private set; }
    public Guid? QuoteId { get; private set; }

    public int Quantity { get; private set; }
    public int ConsumedQuantity { get; private set; }

    public ReservationStatus Status { get; private set; } = ReservationStatus.Active;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private InventoryReservation() { }

    public InventoryReservation(Guid shopId, Guid inventoryItemId, Guid repairOrderId, Guid? quoteId, int quantity, DateTime nowUtc)
    {
        if (inventoryItemId == Guid.Empty) throw new DomainException("La reserva debe referenciar un ítem.");
        if (repairOrderId == Guid.Empty) throw new DomainException("La reserva debe referenciar una orden.");
        if (quantity <= 0) throw new DomainException("La cantidad reservada debe ser mayor a 0.");

        ShopId = shopId;
        InventoryItemId = inventoryItemId;
        RepairOrderId = repairOrderId;
        QuoteId = quoteId;
        Quantity = quantity;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public int RemainingQuantity => Status == ReservationStatus.Active ? Quantity - ConsumedQuantity : 0;

    /// <summary>Consumes up to <paramref name="quantity"/> units and returns how many were covered by the reservation.</summary>
    public int Consume(int quantity, DateTime nowUtc)
    {
        if (Status != ReservationStatus.Active || quantity <= 0) return 0;
        var covered = Math.Min(quantity, RemainingQuantity);
        ConsumedQuantity += covered;
        if (ConsumedQuantity >= Quantity) Status = ReservationStatus.Consumed;
        UpdatedAtUtc = nowUtc;
        return covered;
    }

    public void Release(DateTime nowUtc)
    {
        if (Status != ReservationStatus.Active) return;
        Status = ConsumedQuantity > 0 ? ReservationStatus.Consumed : ReservationStatus.Released;
        UpdatedAtUtc = nowUtc;
    }
}
