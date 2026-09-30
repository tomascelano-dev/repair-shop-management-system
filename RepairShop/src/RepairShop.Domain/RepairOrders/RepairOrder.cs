using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrder
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Multi-sucursal
    public Guid ShopId { get; private set; }

    public Guid CustomerId { get; private set; }
    public Guid DeviceId { get; private set; }

    public string IssueDescription { get; private set; } = null!;
    public string? Notes { get; private set; }

    public RepairOrderStatus Status { get; private set; } = RepairOrderStatus.Received;

    // Quote (presupuesto)
    public decimal? QuoteAmount { get; private set; }
    public string? QuoteCurrency { get; private set; }
    public Guid? QuoteUpdatedByUserId { get; private set; }
    public DateTime? QuoteUpdatedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private RepairOrder() { } // EF

    // Back-compat: constructor anterior (sin ShopId)
    public RepairOrder(Guid customerId, Guid deviceId, string issueDescription, string? notes, DateTime nowUtc)
        : this(Guid.Empty, customerId, deviceId, issueDescription, notes, nowUtc)
    {
    }

    public RepairOrder(Guid shopId, Guid customerId, Guid deviceId, string issueDescription, string? notes, DateTime nowUtc)
    {
        ShopId = shopId;
        CustomerId = customerId;
        DeviceId = deviceId;
        IssueDescription = (issueDescription ?? "").Trim();
        Notes = notes?.Trim();

        if (CustomerId == Guid.Empty) throw new DomainException("RepairOrder must have a CustomerId.");
        if (DeviceId == Guid.Empty) throw new DomainException("RepairOrder must have a DeviceId.");
        if (IssueDescription.Length < 5) throw new DomainException("Issue description is required (min 5 chars).");

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void SetQuote(decimal amount, string currency, Guid updatedByUserId, DateTime nowUtc)
    {
        if (amount <= 0) throw new DomainException("Quote amount must be > 0.");
        currency = (currency ?? "").Trim().ToUpperInvariant();
        if (currency.Length < 3) throw new DomainException("Quote currency is required (e.g. USD, ARS).");
        if (updatedByUserId == Guid.Empty) throw new DomainException("Quote must have an updater user id.");

        QuoteAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        QuoteCurrency = currency;
        QuoteUpdatedByUserId = updatedByUserId;
        QuoteUpdatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void ClearQuote(Guid updatedByUserId, DateTime nowUtc)
    {
        if (updatedByUserId == Guid.Empty) throw new DomainException("Quote must have an updater user id.");
        QuoteAmount = null;
        QuoteCurrency = null;
        QuoteUpdatedByUserId = updatedByUserId;
        QuoteUpdatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(string issueDescription, string? notes, DateTime nowUtc)
    {
        IssueDescription = (issueDescription ?? "").Trim();
        Notes = notes?.Trim();
        if (IssueDescription.Length < 5) throw new DomainException("Issue description is required (min 5 chars).");

        UpdatedAtUtc = nowUtc;
    }

    public void MoveTo(RepairOrderStatus newStatus, DateTime nowUtc)
    {
        if (Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
            throw new DomainException("Finalized orders cannot change status.");

        var valid = (Status, newStatus) switch
        {
            (RepairOrderStatus.Diagnosing, RepairOrderStatus.AwaitingApproval) => true,
            (RepairOrderStatus.AwaitingApproval, RepairOrderStatus.Diagnosing) => true,
            (RepairOrderStatus.AwaitingApproval, RepairOrderStatus.InProgress) => true,
            (RepairOrderStatus.AwaitingApproval, RepairOrderStatus.WaitingParts) => true,
            (RepairOrderStatus.WaitingParts, RepairOrderStatus.InProgress) => true,
            (RepairOrderStatus.WaitingParts, RepairOrderStatus.AwaitingApproval) => true,
            (RepairOrderStatus.InProgress, RepairOrderStatus.WaitingParts) => true,
            (RepairOrderStatus.InProgress, RepairOrderStatus.AwaitingApproval) => true,
            (RepairOrderStatus.InProgress, RepairOrderStatus.QualityCheck) => true,
            (RepairOrderStatus.QualityCheck, RepairOrderStatus.InProgress) => true,
            (RepairOrderStatus.QualityCheck, RepairOrderStatus.Ready) => true,
            (RepairOrderStatus.Ready, RepairOrderStatus.InProgress) => true,
            (RepairOrderStatus.Received,    RepairOrderStatus.Diagnosing) => true,
            (RepairOrderStatus.Diagnosing,  RepairOrderStatus.InProgress) => true,
            (RepairOrderStatus.InProgress,  RepairOrderStatus.Ready)      => true,
            (RepairOrderStatus.Ready,       RepairOrderStatus.Delivered)  => true,

            // cancellation from any non-final state
            (_, RepairOrderStatus.Cancelled) => true,

            _ => false
        };

        if (!valid)
            throw new DomainException($"Invalid status transition: {Status} -> {newStatus}.");

        Status = newStatus;
        UpdatedAtUtc = nowUtc;
    }
}
