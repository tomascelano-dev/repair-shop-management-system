using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderPartUsageRepository
{
    Task AddAsync(RepairOrderPartUsage usage, CancellationToken ct);
    Task<List<RepairOrderPartUsage>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
}
