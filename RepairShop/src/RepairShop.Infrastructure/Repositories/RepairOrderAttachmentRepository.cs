using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderAttachmentRepository : IRepairOrderAttachmentRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderAttachmentRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(RepairOrderAttachment attachment, CancellationToken ct)
        => _db.RepairOrderAttachments.AddAsync(attachment, ct).AsTask();

    public Task<List<RepairOrderAttachment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderAttachments
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
}
