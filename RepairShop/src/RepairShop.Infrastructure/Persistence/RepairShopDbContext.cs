using Microsoft.EntityFrameworkCore;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Infrastructure.Persistence;

public sealed partial class RepairShopDbContext : DbContext
{
    public DbSet<WorkshopWorkflow> Workflows => Set<WorkshopWorkflow>();
    public DbSet<WorkflowQuote> WorkflowQuotes => Set<WorkflowQuote>();
    public DbSet<WorkflowRequest> WorkflowRequests => Set<WorkflowRequest>();
    public DbSet<WorkflowPhoto> WorkflowPhotos => Set<WorkflowPhoto>();
    public DbSet<WorkflowRefund> WorkflowRefunds => Set<WorkflowRefund>();
    public RepairShopDbContext(DbContextOptions<RepairShopDbContext> options) : base(options) { }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();
    public DbSet<RepairOrderStatusHistory> RepairOrderStatusHistory => Set<RepairOrderStatusHistory>();
    public DbSet<RepairOrderNote> RepairOrderNotes => Set<RepairOrderNote>();
    public DbSet<RepairOrderAttachment> RepairOrderAttachments => Set<RepairOrderAttachment>();
    public DbSet<RepairOrderPayment> RepairOrderPayments => Set<RepairOrderPayment>();
    public DbSet<RepairOrderReceptionChecklist> RepairOrderReceptionChecklists => Set<RepairOrderReceptionChecklist>();

    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<NotificationOutboxItem> NotificationOutbox => Set<NotificationOutboxItem>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<RepairOrderPartUsage> RepairOrderPartUsages => Set<RepairOrderPartUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurePremium(modelBuilder);
        ConfigureSaas(modelBuilder);
        modelBuilder.Entity<WorkshopWorkflow>(b => {
            b.ToTable("workflows"); b.HasKey(x => x.Id);
            b.Property(x => x.Number).UseIdentityByDefaultColumn();
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
            b.HasIndex(x => x.PortalTokenHash).IsUnique();
            b.Property(x => x.LaborCost).HasColumnType("numeric(18,2)");
            b.HasOne<RepairOrder>().WithOne().HasForeignKey<WorkshopWorkflow>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkflowQuote>(b => {
            b.ToTable("workflow_quotes"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ShopId, x.OrderId, x.Revision }).IsUnique();
            b.Property(x => x.Total).HasColumnType("numeric(18,2)");
            b.HasOne<WorkshopWorkflow>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkflowRequest>(b => {
            b.ToTable("workflow_requests"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ShopId, x.Operation, x.Key }).IsUnique();
        });
        modelBuilder.Entity<WorkflowPhoto>(b => {
            b.ToTable("workflow_photos"); b.HasKey(x => x.Id);
            b.HasOne<WorkshopWorkflow>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkflowRefund>(b => {
            b.ToTable("workflow_refunds"); b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            b.HasOne<RepairOrderPayment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        // Shops
        modelBuilder.Entity<Shop>(b =>
        {
            b.ToTable("shops");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(40);
            b.Property(x => x.AddressLine).HasMaxLength(200);
            b.Property(x => x.City).HasMaxLength(80);
            b.Property(x => x.Country).HasMaxLength(80);
            b.Property(x => x.IsActive).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.HasIndex(x => x.Name);
        });

        // Users
        modelBuilder.Entity<AppUser>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.Email).HasMaxLength(180).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();

            b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            b.Property(x => x.Role).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(400).IsRequired();

            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        // Customers
        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(40).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);

            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.Phone });
        });

        // Devices
        modelBuilder.Entity<Device>(b =>
        {
            b.ToTable("devices");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.CustomerId).IsRequired();
            b.HasIndex(x => new { x.ShopId, x.CustomerId });

            b.Property(x => x.Brand).HasMaxLength(60).IsRequired();
            b.Property(x => x.Model).HasMaxLength(60).IsRequired();
            b.Property(x => x.Label).HasMaxLength(120);
            b.Property(x => x.SerialNumber).HasMaxLength(80);
            b.Property(x => x.Notes).HasMaxLength(500);

            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        // Orders
        modelBuilder.Entity<RepairOrder>(b =>
        {
            b.ToTable("repair_orders");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.DeviceId).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.CustomerId });
            b.HasIndex(x => new { x.ShopId, x.DeviceId });
            b.HasIndex(x => new { x.ShopId, x.Status });

            b.Property(x => x.IssueDescription).HasMaxLength(500).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(800);

            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.Property(x => x.QuoteAmount).HasColumnType("numeric(18,2)");
            b.Property(x => x.QuoteCurrency).HasMaxLength(8);
            b.Property(x => x.QuoteUpdatedByUserId);
            b.Property(x => x.QuoteUpdatedAtUtc);
        });

        // Status History
        modelBuilder.Entity<RepairOrderStatusHistory>(b =>
        {
            b.ToTable("order_status_history");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.RepairOrderId).IsRequired();
            b.HasIndex(x => new { x.ShopId, x.RepairOrderId });

            b.Property(x => x.FromStatus).IsRequired();
            b.Property(x => x.ToStatus).IsRequired();
            b.Property(x => x.ChangedByUserId).IsRequired();
            b.Property(x => x.ChangedAtUtc).IsRequired();
        });

        // Notes
        modelBuilder.Entity<RepairOrderNote>(b =>
        {
            b.ToTable("order_notes");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.RepairOrderId).IsRequired();
            b.HasIndex(x => new { x.ShopId, x.RepairOrderId });

            b.Property(x => x.Body).HasMaxLength(1200).IsRequired();
            b.Property(x => x.CreatedByUserId).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        // Attachments
        modelBuilder.Entity<RepairOrderAttachment>(b =>
        {
            b.ToTable("order_attachments");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.RepairOrderId).IsRequired();
            b.HasIndex(x => new { x.ShopId, x.RepairOrderId });

            b.Property(x => x.Url).HasMaxLength(800).IsRequired();
            b.Property(x => x.Label).HasMaxLength(120);

            b.Property(x => x.CreatedByUserId).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        // Payments
        modelBuilder.Entity<RepairOrderPayment>(b =>
        {
            b.ToTable("order_payments");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.RepairOrderId).IsRequired();
            b.HasIndex(x => new { x.ShopId, x.RepairOrderId });

            b.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Method).IsRequired();
            b.Property(x => x.Reference).HasMaxLength(120);

            b.Property(x => x.CreatedByUserId).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        // Reception checklist (1 per order)
        modelBuilder.Entity<RepairOrderReceptionChecklist>(b =>
        {
            b.ToTable("order_reception_checklists");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.Property(x => x.RepairOrderId).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.RepairOrderId }).IsUnique();

            b.Property(x => x.ScreenOk).IsRequired();
            b.Property(x => x.CamerasOk).IsRequired();
            b.Property(x => x.SpeakersOk).IsRequired();
            b.Property(x => x.MicrophoneOk).IsRequired();
            b.Property(x => x.ButtonsOk).IsRequired();
            b.Property(x => x.FaceIdOk).IsRequired();
            b.Property(x => x.FingerprintOk).IsRequired();
            b.Property(x => x.CloudLock).IsRequired();
            b.Property(x => x.BatteryPercent);
            b.Property(x => x.CosmeticNotes).HasMaxLength(500);

            b.Property(x => x.UpdatedByUserId).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        // Templates
        modelBuilder.Entity<MessageTemplate>(b =>
        {
            b.ToTable("message_templates");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.Key).HasMaxLength(120).IsRequired();
            b.Property(x => x.Title).HasMaxLength(120).IsRequired();
            b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            b.Property(x => x.IsActive).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.Key }).IsUnique();
        });

        // Audit
        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.ToTable("audit_events");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            b.Property(x => x.EntityId).IsRequired();
            b.Property(x => x.Action).HasMaxLength(120).IsRequired();
            b.Property(x => x.ActorUserId);
            b.Property(x => x.ActorEmail).HasMaxLength(180);
            b.Property(x => x.DataJson).HasMaxLength(4000);
            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.EntityType, x.EntityId, x.CreatedAtUtc });
        });

        // Outbox
        modelBuilder.Entity<NotificationOutboxItem>(b =>
        {
            b.ToTable("notification_outbox");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.Channel).IsRequired();
            b.Property(x => x.Recipient).HasMaxLength(80).IsRequired();
            b.Property(x => x.Title).HasMaxLength(120);
            b.Property(x => x.Body).HasMaxLength(4000).IsRequired();

            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.LastError).HasMaxLength(4000);
            b.Property(x => x.AttemptCount).IsRequired();
            b.Property(x => x.NextAttemptAtUtc);
            b.Property(x => x.CorrelationKey).HasMaxLength(200);
            b.Property(x => x.RelatedEntityType).HasMaxLength(80);
            b.Property(x => x.RelatedEntityId);

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.Status, x.CreatedAtUtc });
            b.HasIndex(x => new { x.ShopId, x.CorrelationKey }).IsUnique();
        });

        // Inventory items
        modelBuilder.Entity<InventoryItem>(b =>
        {
            b.ToTable("inventory_items");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.QuantityOnHand).IsRequired();

            b.Property(x => x.UnitCost).HasColumnType("numeric(18,2)");
            b.Property(x => x.UnitCostCurrency).HasMaxLength(8);
            b.Property(x => x.IsActive).IsRequired();

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.Sku }).IsUnique();
        });

        // Inventory adjustments
        modelBuilder.Entity<InventoryAdjustment>(b =>
        {
            b.ToTable("inventory_adjustments");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.InventoryItemId).IsRequired();
            b.HasIndex(x => new { x.ShopId, x.InventoryItemId });

            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.DeltaQuantity).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(300);
            b.Property(x => x.RepairOrderId);

            b.Property(x => x.CreatedByUserId).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        // Part usage
        modelBuilder.Entity<RepairOrderPartUsage>(b =>
        {
            b.ToTable("order_part_usage");
            b.HasKey(x => x.Id);

            b.Property(x => x.ShopId).IsRequired();
            b.HasIndex(x => x.ShopId);

            b.Property(x => x.RepairOrderId).IsRequired();
            b.Property(x => x.InventoryItemId).IsRequired();

            b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
            b.HasIndex(x => new { x.ShopId, x.InventoryItemId });

            b.Property(x => x.QuantityUsed).IsRequired();
            b.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
            b.Property(x => x.UnitPriceCurrency).HasMaxLength(8);
            b.Property(x => x.CreatedByUserId).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });
    }
}
