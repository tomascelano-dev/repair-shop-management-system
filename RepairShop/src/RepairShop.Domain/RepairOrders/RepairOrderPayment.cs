using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrderPayment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }

    public string? Reference { get; private set; }

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
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = (currency ?? "").Trim().ToUpperInvariant();
        Method = method;
        Reference = reference?.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("Payment must belong to an order.");
        if (CreatedByUserId == Guid.Empty) throw new DomainException("Payment must have an author user id.");
        if (Amount <= 0) throw new DomainException("Payment amount must be > 0.");
        if (Currency.Length < 3) throw new DomainException("Payment currency is required (e.g. USD, ARS).");
    }
}
