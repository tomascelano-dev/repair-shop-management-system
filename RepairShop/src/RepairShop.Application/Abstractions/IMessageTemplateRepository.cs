using RepairShop.Domain.Messaging;

namespace RepairShop.Application.Abstractions;

public interface IMessageTemplateRepository
{
    Task<MessageTemplate?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<MessageTemplate?> GetByKeyAsync(Guid shopId, string key, CancellationToken ct);
    Task<List<MessageTemplate>> ListAsync(Guid shopId, bool includeInactive, CancellationToken ct);
    Task AddAsync(MessageTemplate template, CancellationToken ct);
    Task RemoveAsync(MessageTemplate template, CancellationToken ct);
}
