using RepairShop.Domain.Common;

namespace RepairShop.Domain.Payments;

public enum PaymentLinkStatus
{
    Pending = 0,
    Paid = 1,
    Expired = 2,
    Cancelled = 3
}

/// <summary>
/// Online payment link (Mercado Pago Checkout Pro) to collect the balance of a repair order.
/// </summary>
public sealed class PaymentLink : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string Provider { get; private set; } = "mercadopago";
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Title { get; private set; } = null!;

    public PaymentLinkStatus Status { get; private set; } = PaymentLinkStatus.Pending;
    public string? ExternalId { get; private set; }
    public string? Url { get; private set; }
    public string? ExternalPaymentId { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PaymentLink() { }

    public PaymentLink(Guid shopId, string entityType, Guid entityId, decimal amount, string currency, string title, DateTime expiresAtUtc, Guid? userId, DateTime nowUtc)
    {
        if (entityId == Guid.Empty) throw new DomainException("El link de pago debe referenciar una entidad.");
        if (amount <= 0) throw new DomainException("El importe a cobrar debe ser mayor a 0.");
        if (expiresAtUtc <= nowUtc) throw new DomainException("El vencimiento del link debe ser futuro.");

        ShopId = shopId;
        EntityType = entityType;
        EntityId = entityId;
        Amount = Money.Round(amount);
        Currency = Money.NormalizeCurrency(currency);
        Title = string.IsNullOrWhiteSpace(title) ? "Pago" : title.Trim()[..Math.Min(title.Trim().Length, 120)];
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = userId == Guid.Empty ? null : userId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public bool IsUsable(DateTime nowUtc) => Status == PaymentLinkStatus.Pending && ExpiresAtUtc > nowUtc && Url is not null;

    public void AttachProviderData(string externalId, string url, DateTime nowUtc)
    {
        ExternalId = externalId;
        Url = url;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkPaid(string externalPaymentId, DateTime nowUtc)
    {
        if (Status == PaymentLinkStatus.Paid) return;
        Status = PaymentLinkStatus.Paid;
        ExternalPaymentId = externalPaymentId;
        PaidAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status != PaymentLinkStatus.Pending) return;
        Status = PaymentLinkStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }
}
