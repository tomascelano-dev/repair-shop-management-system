using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Auditing;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class AuditEventRepository : IAuditEventRepository
{
    private readonly RepairShopDbContext _db;
    public AuditEventRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(AuditEvent evt, CancellationToken ct)
        => _db.AuditEvents.AddAsync(evt, ct).AsTask();

    public Task<List<AuditEvent>> ListByEntityAsync(Guid shopId, string entityType, Guid entityId, int skip, int take, CancellationToken ct)
        => _db.AuditEvents
            .Where(x => x.ShopId == shopId && x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
}
