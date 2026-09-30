using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrderNote
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public string Body { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RepairOrderNote() { } // EF

    public RepairOrderNote(Guid shopId, Guid repairOrderId, string body, Guid createdByUserId, DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        Body = (body ?? "").Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("Note must belong to an order.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("Note must have an author user id.");
        if (Body.Length < 2) throw new DomainException("Note body is required (min 2 chars).");
    }

    // Back-compat
    public RepairOrderNote(Guid repairOrderId, string body, Guid createdByUserId, DateTime nowUtc)
        : this(Guid.Empty, repairOrderId, body, createdByUserId, nowUtc)
    {
    }
}
