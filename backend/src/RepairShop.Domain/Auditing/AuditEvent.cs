using RepairShop.Domain.Common;

namespace RepairShop.Domain.Auditing;

public sealed class AuditEvent : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }

    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }

    public string Action { get; private set; } = null!;

    public Guid? ActorUserId { get; private set; }
    public string? ActorEmail { get; private set; }

    public string? DataJson { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private AuditEvent() { } // EF

    public AuditEvent(
        Guid shopId,
        string entityType,
        Guid entityId,
        string action,
        Guid? actorUserId,
        string? actorEmail,
        string? dataJson,
        DateTime nowUtc)
    {
        ShopId = shopId;
        EntityType = (entityType ?? "").Trim();
        EntityId = entityId;
        Action = (action ?? "").Trim();
        ActorUserId = actorUserId == Guid.Empty ? null : actorUserId;
        ActorEmail = actorEmail?.Trim();
        DataJson = dataJson is { Length: > 4000 } ? dataJson[..4000] : dataJson;
        CreatedAtUtc = nowUtc;

        if (EntityType.Length < 2) throw new DomainException("Falta el tipo de entidad del evento de auditoría.");
        if (EntityId == Guid.Empty) throw new DomainException("Falta la entidad del evento de auditoría.");
        if (Action.Length < 2) throw new DomainException("Falta la acción del evento de auditoría.");
    }
}
