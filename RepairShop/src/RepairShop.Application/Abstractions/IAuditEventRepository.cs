using RepairShop.Domain.Auditing;

namespace RepairShop.Application.Abstractions;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent evt, CancellationToken ct);
    Task<List<AuditEvent>> ListByEntityAsync(Guid shopId, string entityType, Guid entityId, int skip, int take, CancellationToken ct);
}
