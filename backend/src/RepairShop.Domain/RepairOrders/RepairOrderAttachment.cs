using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public enum AttachmentKind
{
    Link = 0,
    Photo = 1,
    Document = 2
}

public sealed class RepairOrderAttachment : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public AttachmentKind Kind { get; private set; } = AttachmentKind.Link;

    // External link (Kind = Link)
    public string? Url { get; private set; }

    // Uploaded file (Kind = Photo/Document)
    public Guid? FileId { get; private set; }

    public string? Label { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RepairOrderAttachment() { } // EF

    public RepairOrderAttachment(Guid shopId, Guid repairOrderId, string url, string? label, Guid createdByUserId, DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        Kind = AttachmentKind.Link;
        Url = (url ?? "").Trim();
        Label = CleanLabel(label);
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        Validate();
        if (Url.Length < 8) throw new DomainException("La URL del adjunto es obligatoria.");
        if (Url.Length > 800) throw new DomainException("La URL del adjunto es demasiado larga.");
        if (!Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("La URL del adjunto debe empezar con http:// o https://");
    }

    public static RepairOrderAttachment ForFile(Guid shopId, Guid repairOrderId, AttachmentKind kind, Guid fileId, string? label, Guid createdByUserId, DateTime nowUtc)
    {
        if (kind == AttachmentKind.Link) throw new DomainException("Un archivo no puede ser de tipo link.");
        if (fileId == Guid.Empty) throw new DomainException("Falta el archivo.");

        var a = new RepairOrderAttachment
        {
            ShopId = shopId,
            RepairOrderId = repairOrderId,
            Kind = kind,
            FileId = fileId,
            Label = CleanLabel(label),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
        a.Validate();
        return a;
    }

    // Back-compat
    public RepairOrderAttachment(Guid repairOrderId, string url, string? label, Guid createdByUserId, DateTime nowUtc)
        : this(Guid.Empty, repairOrderId, url, label, createdByUserId, nowUtc)
    {
    }

    private void Validate()
    {
        if (RepairOrderId == Guid.Empty) throw new DomainException("El adjunto debe pertenecer a una orden.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("El adjunto debe tener un autor.");
    }

    private static string? CleanLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        label = label.Trim();
        return label.Length > 120 ? label[..120] : label;
    }
}
