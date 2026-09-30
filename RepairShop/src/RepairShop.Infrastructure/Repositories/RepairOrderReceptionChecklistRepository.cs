using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderReceptionChecklistRepository : IRepairOrderReceptionChecklistRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderReceptionChecklistRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrderReceptionChecklist?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderReceptionChecklists.FirstOrDefaultAsync(x => x.ShopId == shopId && x.RepairOrderId == orderId, ct);

    public Task AddAsync(RepairOrderReceptionChecklist checklist, CancellationToken ct)
        => _db.RepairOrderReceptionChecklists.AddAsync(checklist, ct).AsTask();
}
