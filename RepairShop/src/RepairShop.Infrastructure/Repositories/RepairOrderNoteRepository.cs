using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderNoteRepository : IRepairOrderNoteRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderNoteRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(RepairOrderNote note, CancellationToken ct)
        => _db.RepairOrderNotes.AddAsync(note, ct).AsTask();

    public Task<List<RepairOrderNote>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderNotes
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
}
