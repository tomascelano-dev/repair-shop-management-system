using RepairShop.Domain.Common;

namespace RepairShop.Domain.Feedback;

/// <summary>
/// Post-delivery satisfaction survey answer (1-5 stars + optional comment). One per order.
/// </summary>
public sealed class CustomerFeedback : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int Score { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CustomerFeedback() { }

    public CustomerFeedback(Guid shopId, Guid repairOrderId, Guid customerId, int score, string? comment, DateTime nowUtc)
    {
        if (repairOrderId == Guid.Empty) throw new DomainException("La encuesta debe referenciar una orden.");
        if (score is < 1 or > 5) throw new DomainException("La puntuación debe ser de 1 a 5.");

        ShopId = shopId;
        RepairOrderId = repairOrderId;
        CustomerId = customerId;
        Score = score;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()[..Math.Min(comment.Trim().Length, 1000)];
        CreatedAtUtc = nowUtc;
    }
}
