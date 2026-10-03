using RepairShop.Application.Common;

namespace RepairShop.Application.Abstractions;

/// <summary>Convenience wrapper to append audit events (persisted with the next SaveChanges).</summary>
public interface IAuditLog
{
    Task AddAsync(Guid shopId, string entityType, Guid entityId, string action, Actor actor, object? data, CancellationToken ct);
}
