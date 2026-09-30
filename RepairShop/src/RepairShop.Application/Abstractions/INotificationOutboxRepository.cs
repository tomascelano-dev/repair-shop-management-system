using RepairShop.Domain.Notifications;

namespace RepairShop.Application.Abstractions;

public interface INotificationOutboxRepository
{
    Task AddAsync(NotificationOutboxItem item, CancellationToken ct);
    Task<NotificationOutboxItem?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<NotificationOutboxItem>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct);
}
