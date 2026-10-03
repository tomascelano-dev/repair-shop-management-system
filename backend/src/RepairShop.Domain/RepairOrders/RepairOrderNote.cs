using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrderNote : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public string Body { get; private set; } = null!;

    // Public notes are visible to the customer in the tracking portal; internal ones are staff-only.
    public bool IsPublic { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RepairOrderNote() { } // EF

    public RepairOrderNote(Guid shopId, Guid repairOrderId, string body, Guid createdByUserId, DateTime nowUtc)
        : this(shopId, repairOrderId, body, isPublic: false, createdByUserId, nowUtc)
    {
    }

    public RepairOrderNote(Guid shopId, Guid repairOrderId, string body, bool isPublic, Guid createdByUserId, DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        Body = (body ?? "").Trim();
        IsPublic = isPublic;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("La nota debe pertenecer a una orden.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("La nota debe tener un autor.");
        if (Body.Length < 2) throw new DomainException("La nota es obligatoria (mín. 2 caracteres).");
        if (Body.Length > 1200) throw new DomainException("La nota es demasiado larga (máx. 1200).");
    }

    // Back-compat
    public RepairOrderNote(Guid repairOrderId, string body, Guid createdByUserId, DateTime nowUtc)
        : this(Guid.Empty, repairOrderId, body, createdByUserId, nowUtc)
    {
    }
}
