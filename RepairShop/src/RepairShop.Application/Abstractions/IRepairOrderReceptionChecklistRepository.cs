using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderReceptionChecklistRepository
{
    Task<RepairOrderReceptionChecklist?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrderReceptionChecklist checklist, CancellationToken ct);
}
