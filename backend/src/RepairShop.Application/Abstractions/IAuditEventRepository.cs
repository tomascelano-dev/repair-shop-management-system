using RepairShop.Domain.Auditing;

namespace RepairShop.Application.Abstractions;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent evt, CancellationToken ct);
    Task<List<AuditEvent>> ListByEntityAsync(Guid shopId, string entityType, Guid entityId, int skip, int take, CancellationToken ct);
    Task<(List<AuditEvent> Items, int Total)> SearchAsync(Guid shopId, AuditSearchOptions options, CancellationToken ct);
}

public sealed record AuditSearchOptions(
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    Guid? ActorUserId = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    int Skip = 0,
    int Take = 50);
