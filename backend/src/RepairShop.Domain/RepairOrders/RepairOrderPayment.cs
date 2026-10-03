using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public enum PaymentType
{
    Payment = 0,
    Refund = 1
}

public sealed class RepairOrderPayment : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public PaymentType Type { get; private set; } = PaymentType.Payment;

    // Always positive. Refunds subtract from the paid total.
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }

    // "Seña": a payment registered before the price was agreed.
    public bool IsDeposit { get; private set; }

    public Guid? CashSessionId { get; private set; }

    // Provider payment id (e.g. Mercado Pago) used for idempotent reconciliation.
    public string? ExternalPaymentId { get; private set; }

    public Guid? RefundOfPaymentId { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RepairOrderPayment() { } // EF

    public RepairOrderPayment(
        Guid shopId,
        Guid repairOrderId,
        decimal amount,
        string currency,
        PaymentMethod method,
        string? reference,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        Amount = Money.Round(amount);
        Currency = Money.NormalizeCurrency(currency);
        Method = method;
        Reference = Clean(reference, 120);
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("El pago debe pertenecer a una orden.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("El pago debe tener un usuario.");
        if (Amount <= 0) throw new DomainException("El importe debe ser mayor a 0.");
        if (!Enum.IsDefined(method)) throw new DomainException("Medio de pago inválido.");
    }

    public static RepairOrderPayment CreateRefund(RepairOrderPayment original, decimal amount, PaymentMethod method, string? reason, Guid userId, Guid? cashSessionId, DateTime nowUtc)
    {
        if (original.Type != PaymentType.Payment) throw new DomainException("Solo se puede devolver un pago.");
        var refund = new RepairOrderPayment(original.ShopId, original.RepairOrderId, amount, original.Currency, method, reason, userId, nowUtc)
        {
            Type = PaymentType.Refund,
            RefundOfPaymentId = original.Id,
            CashSessionId = cashSessionId
        };
        if (refund.Amount > original.Amount) throw new DomainException("La devolución no puede superar el pago original.");
        return refund;
    }

    public decimal SignedAmount => Type == PaymentType.Refund ? -Amount : Amount;

    public void MarkAsDeposit() => IsDeposit = true;

    public void LinkCashSession(Guid? cashSessionId) => CashSessionId = cashSessionId;

    public void SetExternalPaymentId(string externalPaymentId)
    {
        externalPaymentId = (externalPaymentId ?? "").Trim();
        if (externalPaymentId.Length is < 1 or > 80) throw new DomainException("Id de pago externo inválido.");
        ExternalPaymentId = externalPaymentId;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
