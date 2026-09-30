using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderAttachmentRepository
{
    Task<List<RepairOrderAttachment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrderAttachment attachment, CancellationToken ct);
}
