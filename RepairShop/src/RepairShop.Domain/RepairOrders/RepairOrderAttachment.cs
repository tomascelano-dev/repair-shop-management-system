using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrderAttachment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public string Url { get; private set; } = null!;
    public string? Label { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RepairOrderAttachment() { } // EF

    public RepairOrderAttachment(Guid shopId, Guid repairOrderId, string url, string? label, Guid createdByUserId, DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        Url = (url ?? "").Trim();
        Label = label?.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("Attachment must belong to an order.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("Attachment must have an author user id.");
        if (Url.Length < 8) throw new DomainException("Attachment url is required.");
        if (!Url.StartsWith("http://") && !Url.StartsWith("https://"))
            throw new DomainException("Attachment url must start with http:// or https://");
    }

    // Back-compat
    public RepairOrderAttachment(Guid repairOrderId, string url, string? label, Guid createdByUserId, DateTime nowUtc)
        : this(Guid.Empty, repairOrderId, url, label, createdByUserId, nowUtc)
    {
    }
}
