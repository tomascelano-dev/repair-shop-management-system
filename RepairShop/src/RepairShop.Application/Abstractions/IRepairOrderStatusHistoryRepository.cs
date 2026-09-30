using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderStatusHistoryRepository
{
    Task<List<RepairOrderStatusHistory>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrderStatusHistory history, CancellationToken ct);
}
