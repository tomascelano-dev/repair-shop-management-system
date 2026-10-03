using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.Notifications;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> b)
    {
        b.ToTable("message_templates");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.Key).HasMaxLength(120).IsRequired();
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.Key }).IsUnique();
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityId).IsRequired();
        b.Property(x => x.Action).HasMaxLength(120).IsRequired();
        b.Property(x => x.ActorEmail).HasMaxLength(180);
        b.Property(x => x.DataJson).HasMaxLength(4000);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.EntityType, x.EntityId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
    }
}

internal sealed class NotificationOutboxItemConfiguration : IEntityTypeConfiguration<NotificationOutboxItem>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxItem> b)
    {
        b.ToTable("notification_outbox");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.Channel).IsRequired();
        b.Property(x => x.Recipient).HasMaxLength(180).IsRequired();
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(4000);
        b.Property(x => x.CorrelationKey).HasMaxLength(200);
        b.Property(x => x.TemplateKey).HasMaxLength(120);
        b.Property(x => x.RelatedEntityType).HasMaxLength(80);
        b.Property(x => x.Provider).HasMaxLength(40);
        b.Property(x => x.ProviderMessageId).HasMaxLength(200);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.Status, x.CreatedAtUtc });
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.HasIndex(x => new { x.ShopId, x.RelatedEntityType, x.RelatedEntityId });
        b.HasIndex(x => new { x.ShopId, x.CorrelationKey }).IsUnique();
    }
}
