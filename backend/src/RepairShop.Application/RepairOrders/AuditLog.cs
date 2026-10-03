using System.Text.Json;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Domain.Auditing;

namespace RepairShop.Application.RepairOrders;

public sealed class AuditLog : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAuditEventRepository _repo;
    private readonly IDateTimeProvider _clock;

    public AuditLog(IAuditEventRepository repo, IDateTimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public Task AddAsync(Guid shopId, string entityType, Guid entityId, string action, Actor actor, object? data, CancellationToken ct)
        => _repo.AddAsync(new AuditEvent(shopId, entityType, entityId, action,
            actor.IsSystem ? null : actor.UserId, actor.Email,
            data is null ? null : JsonSerializer.Serialize(data, Json), _clock.UtcNow), ct);
}
