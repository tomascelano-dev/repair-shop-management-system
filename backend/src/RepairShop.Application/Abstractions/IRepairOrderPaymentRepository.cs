using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderPaymentRepository
{
    Task<RepairOrderPayment?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<RepairOrderPayment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);

    /// <summary>Net paid (payments - refunds) for the order in the given currency.</summary>
    Task<decimal> SumByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);

    Task<bool> ExistsExternalAsync(Guid shopId, string externalPaymentId, CancellationToken ct);
    Task<decimal> SumRefundsAsync(Guid shopId, Guid paymentId, CancellationToken ct);
    Task AddAsync(RepairOrderPayment payment, CancellationToken ct);
}
