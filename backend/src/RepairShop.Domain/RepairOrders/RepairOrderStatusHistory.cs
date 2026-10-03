using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrderStatusHistory : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public RepairOrderStatus FromStatus { get; private set; }
    public RepairOrderStatus ToStatus { get; private set; }

    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    public string? Reason { get; private set; }

    private RepairOrderStatusHistory() { } // EF

    public RepairOrderStatusHistory(
        Guid shopId,
        Guid repairOrderId,
        RepairOrderStatus fromStatus,
        RepairOrderStatus toStatus,
        Guid changedByUserId,
        DateTime changedAtUtc,
        string? reason = null)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = changedAtUtc;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 300)];
    }

    // Back-compat
    public RepairOrderStatusHistory(Guid repairOrderId, RepairOrderStatus fromStatus, RepairOrderStatus toStatus, Guid changedByUserId, DateTime changedAtUtc)
        : this(Guid.Empty, repairOrderId, fromStatus, toStatus, changedByUserId, changedAtUtc)
    {
    }
}
