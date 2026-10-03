using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Domain.Cash;

// NOTE: persisted as integers.
public enum CashMovementType
{
    Sale = 0,
    OrderPayment = 1,
    Income = 2,
    Expense = 3,
    Withdrawal = 4,
    Refund = 5
}

public sealed class CashMovement : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid SessionId { get; private set; }

    public CashMovementType Type { get; private set; }
    public PaymentMethod Method { get; private set; }

    // Always positive; direction comes from the type.
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;

    public string Description { get; private set; } = null!;
    public string? Category { get; private set; }

    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CashMovement() { }

    public CashMovement(
        Guid shopId,
        Guid sessionId,
        CashMovementType type,
        PaymentMethod method,
        decimal amount,
        string currency,
        string description,
        string? category,
        string? relatedEntityType,
        Guid? relatedEntityId,
        Guid userId,
        DateTime nowUtc)
    {
        if (sessionId == Guid.Empty) throw new DomainException("El movimiento debe pertenecer a una caja.");
        if (!Enum.IsDefined(type)) throw new DomainException("Tipo de movimiento inválido.");
        if (!Enum.IsDefined(method)) throw new DomainException("Medio de pago inválido.");
        if (amount <= 0) throw new DomainException("El importe debe ser mayor a 0.");
        description = (description ?? "").Trim();
        if (description.Length < 2) throw new DomainException("Indicá una descripción del movimiento.");
        if (userId == Guid.Empty) throw new DomainException("El movimiento debe tener un usuario.");

        ShopId = shopId;
        SessionId = sessionId;
        Type = type;
        Method = method;
        Amount = Money.Round(amount);
        Currency = Money.NormalizeCurrency(currency);
        Description = description.Length > 200 ? description[..200] : description;
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim()[..Math.Min(category.Trim().Length, 60)];
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
    }

    public bool IsInflow => Type is CashMovementType.Sale or CashMovementType.OrderPayment or CashMovementType.Income;

    public decimal SignedAmount => IsInflow ? Amount : -Amount;
}
