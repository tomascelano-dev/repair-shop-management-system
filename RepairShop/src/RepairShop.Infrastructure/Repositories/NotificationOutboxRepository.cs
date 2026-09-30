using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Notifications;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class NotificationOutboxRepository : INotificationOutboxRepository
{
    private readonly RepairShopDbContext _db;
    public NotificationOutboxRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(NotificationOutboxItem item, CancellationToken ct)
        => _db.NotificationOutbox.AddAsync(item, ct).AsTask();

    public Task<NotificationOutboxItem?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.NotificationOutbox.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<List<NotificationOutboxItem>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
        => _db.NotificationOutbox
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
}
