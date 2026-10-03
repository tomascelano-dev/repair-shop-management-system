using RepairShop.Domain.Common;

namespace RepairShop.Domain.Quotes;

// NOTE: persisted as integers.
public enum QuoteStatus
{
    Draft = 0,
    Sent = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
    Superseded = 5
}

public enum QuoteItemKind
{
    Labor = 0,
    Part = 1,
    Other = 2
}

public enum QuoteDecisionSource
{
    Staff = 0,
    CustomerPortal = 1
}

/// <summary>
/// Repair quote (presupuesto) with line items. An order can have several versions;
/// only one can be approved at a time (approving a new version supersedes the previous one).
/// </summary>
public sealed class Quote : IShopScoped
{
    private readonly List<QuoteItem> _items = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }
    public int Version { get; private set; }

    public QuoteStatus Status { get; private set; } = QuoteStatus.Draft;
    public string Currency { get; private set; } = null!;

    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Total { get; private set; }

    public DateTime? ValidUntilUtc { get; private set; }
    public int? WarrantyDays { get; private set; }

    // Visible to the customer
    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public QuoteDecisionSource? DecisionSource { get; private set; }
    public string? DecisionNote { get; private set; }
    public string? DecisionIp { get; private set; }

    public IReadOnlyList<QuoteItem> Items => _items;

    private Quote() { }

    public Quote(Guid shopId, Guid repairOrderId, int version, string currency, Guid userId, DateTime nowUtc)
    {
        if (repairOrderId == Guid.Empty) throw new DomainException("El presupuesto debe pertenecer a una orden.");
        if (version <= 0) throw new DomainException("Versión de presupuesto inválida.");
        if (userId == Guid.Empty) throw new DomainException("El presupuesto debe tener un usuario.");

        ShopId = shopId;
        RepairOrderId = repairOrderId;
        Version = version;
        Currency = Money.NormalizeCurrency(currency);
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public bool IsOpen => Status is QuoteStatus.Draft or QuoteStatus.Sent;

    public void Edit(
        string currency,
        IEnumerable<QuoteItemInput> items,
        decimal discountAmount,
        int? warrantyDays,
        string? notes,
        DateTime nowUtc)
    {
        if (Status != QuoteStatus.Draft) throw new DomainException("Solo se pueden editar presupuestos en borrador.");
        if (discountAmount < 0) throw new DomainException("El descuento no puede ser negativo.");
        if (warrantyDays is < 0 or > 3650) throw new DomainException("La garantía debe estar entre 0 y 3650 días.");

        var list = items.ToList();
        if (list.Count > 50) throw new DomainException("El presupuesto admite hasta 50 ítems.");

        Currency = Money.NormalizeCurrency(currency);
        _items.Clear();
        var position = 1;
        foreach (var i in list) _items.Add(new QuoteItem(Id, position++, i));

        Subtotal = Money.Round(_items.Sum(x => x.LineTotal));
        DiscountAmount = Money.Round(discountAmount);
        if (DiscountAmount > Subtotal) throw new DomainException("El descuento no puede superar el subtotal.");
        Total = Money.Round(Subtotal - DiscountAmount);
        WarrantyDays = warrantyDays;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 2000)];
        UpdatedAtUtc = nowUtc;
    }

    public void Send(DateTime validUntilUtc, DateTime nowUtc)
    {
        if (Status != QuoteStatus.Draft) throw new DomainException("Solo se puede enviar un presupuesto en borrador.");
        EnsureHasItems();
        if (validUntilUtc <= nowUtc) throw new DomainException("La validez del presupuesto debe ser futura.");

        Status = QuoteStatus.Sent;
        SentAtUtc = nowUtc;
        ValidUntilUtc = validUntilUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Approve(QuoteDecisionSource source, Guid? userId, string? note, string? ip, DateTime nowUtc)
    {
        if (source == QuoteDecisionSource.CustomerPortal && Status != QuoteStatus.Sent)
            throw new DomainException("Este presupuesto ya no está disponible para aprobar.");
        if (!IsOpen) throw new DomainException("El presupuesto ya fue decidido.");
        if (Status == QuoteStatus.Sent && ValidUntilUtc is not null && ValidUntilUtc < nowUtc)
            throw new DomainException("El presupuesto está vencido. Pedí uno nuevo al local.");
        EnsureHasItems();

        Decide(QuoteStatus.Approved, source, userId, note, ip, nowUtc);
    }

    public void Reject(QuoteDecisionSource source, Guid? userId, string? note, string? ip, DateTime nowUtc)
    {
        if (source == QuoteDecisionSource.CustomerPortal && Status != QuoteStatus.Sent)
            throw new DomainException("Este presupuesto ya no está disponible.");
        if (!IsOpen) throw new DomainException("El presupuesto ya fue decidido.");
        Decide(QuoteStatus.Rejected, source, userId, note, ip, nowUtc);
    }

    public bool ExpireIfDue(DateTime nowUtc)
    {
        if (Status != QuoteStatus.Sent || ValidUntilUtc is null || ValidUntilUtc >= nowUtc) return false;
        Status = QuoteStatus.Expired;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void Supersede(DateTime nowUtc)
    {
        if (Status is QuoteStatus.Approved or QuoteStatus.Draft or QuoteStatus.Sent)
        {
            Status = QuoteStatus.Superseded;
            UpdatedAtUtc = nowUtc;
        }
    }

    private void Decide(QuoteStatus status, QuoteDecisionSource source, Guid? userId, string? note, string? ip, DateTime nowUtc)
    {
        Status = status;
        DecisionSource = source;
        DecidedByUserId = userId == Guid.Empty ? null : userId;
        DecidedAtUtc = nowUtc;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)];
        DecisionIp = string.IsNullOrWhiteSpace(ip) ? null : ip[..Math.Min(ip.Length, 64)];
        UpdatedAtUtc = nowUtc;
    }

    private void EnsureHasItems()
    {
        if (_items.Count == 0) throw new DomainException("El presupuesto debe tener al menos un ítem.");
    }
}

public sealed record QuoteItemInput(
    QuoteItemKind Kind,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? InventoryItemId = null,
    int? WarrantyDays = null);

public sealed class QuoteItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid QuoteId { get; private set; }
    public int Position { get; private set; }
    public QuoteItemKind Kind { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? InventoryItemId { get; private set; }
    public int? WarrantyDays { get; private set; }

    private QuoteItem() { }

    internal QuoteItem(Guid quoteId, int position, QuoteItemInput input)
    {
        var description = (input.Description ?? "").Trim();
        if (description.Length < 2) throw new DomainException("Cada ítem del presupuesto necesita una descripción.");
        if (description.Length > 200) throw new DomainException("La descripción del ítem es demasiado larga (máx. 200).");
        if (input.Quantity <= 0) throw new DomainException("La cantidad debe ser mayor a 0.");
        if (input.UnitPrice < 0) throw new DomainException("El precio no puede ser negativo.");
        if (!Enum.IsDefined(input.Kind)) throw new DomainException("Tipo de ítem inválido.");
        if (input.Kind != QuoteItemKind.Part && input.InventoryItemId is not null)
            throw new DomainException("Solo los repuestos pueden vincularse al inventario.");
        if (input.InventoryItemId is not null && input.Quantity != decimal.Truncate(input.Quantity))
            throw new DomainException("La cantidad de un repuesto de inventario debe ser entera.");
        if (input.WarrantyDays is < 0 or > 3650) throw new DomainException("La garantía debe estar entre 0 y 3650 días.");

        QuoteId = quoteId;
        Position = position;
        Kind = input.Kind;
        Description = description;
        Quantity = decimal.Round(input.Quantity, 3);
        UnitPrice = Money.Round(input.UnitPrice);
        LineTotal = Money.Round(Quantity * UnitPrice);
        InventoryItemId = input.InventoryItemId == Guid.Empty ? null : input.InventoryItemId;
        WarrantyDays = input.WarrantyDays;
    }
}
