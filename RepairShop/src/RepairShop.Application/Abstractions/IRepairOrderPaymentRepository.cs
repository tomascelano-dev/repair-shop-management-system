using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderPaymentRepository
{
    Task<List<RepairOrderPayment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task<decimal> SumByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrderPayment payment, CancellationToken ct);
}
