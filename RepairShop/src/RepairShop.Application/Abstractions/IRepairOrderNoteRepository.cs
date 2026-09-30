using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderNoteRepository
{
    Task<List<RepairOrderNote>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrderNote note, CancellationToken ct);
}
