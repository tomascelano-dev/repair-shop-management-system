using System.Reflection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Common;
using RepairShop.Domain.Currency;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Feedback;
using RepairShop.Domain.Files;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.Payments;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Sales;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Infrastructure.Persistence;

public sealed class RepairShopDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly ITenantContext _tenant;

    public RepairShopDbContext(DbContextOptions<RepairShopDbContext> options)
        : this(options, NoTenantContext.Instance)
    {
    }

    public RepairShopDbContext(DbContextOptions<RepairShopDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant ?? NoTenantContext.Instance;
    }

    /// <summary>
    /// Shop of the current request. Used by the global query filters (EF parameterizes it per query).
    /// Null => no tenant filter (system work); repositories still filter explicitly.
    /// </summary>
    public Guid? CurrentShopId => _tenant.ShopId;

    // Core
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<ShopIntegration> ShopIntegrations => Set<ShopIntegration>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserShopAccess> UserShopAccess => Set<UserShopAccess>();
    public DbSet<ShopCounter> ShopCounters => Set<ShopCounter>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // CRM
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<CustomerFeedback> CustomerFeedback => Set<CustomerFeedback>();

    // Orders
    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();
    public DbSet<RepairOrderStatusHistory> RepairOrderStatusHistory => Set<RepairOrderStatusHistory>();
    public DbSet<RepairOrderNote> RepairOrderNotes => Set<RepairOrderNote>();
    public DbSet<RepairOrderAttachment> RepairOrderAttachments => Set<RepairOrderAttachment>();
    public DbSet<RepairOrderPayment> RepairOrderPayments => Set<RepairOrderPayment>();
    public DbSet<RepairOrderReceptionChecklist> RepairOrderReceptionChecklists => Set<RepairOrderReceptionChecklist>();
    public DbSet<RepairOrderQaChecklist> RepairOrderQaChecklists => Set<RepairOrderQaChecklist>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();

    // Messaging / audit
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<NotificationOutboxItem> NotificationOutbox => Set<NotificationOutboxItem>();

    // Inventory
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<RepairOrderPartUsage> RepairOrderPartUsages => Set<RepairOrderPartUsage>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryItemCompatibility> InventoryItemCompatibilities => Set<InventoryItemCompatibility>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();

    // Sales / cash / payments / fiscal
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleRefund> SaleRefunds => Set<SaleRefund>();
    public DbSet<CashRegisterSession> CashSessions => Set<CashRegisterSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<PaymentLink> PaymentLinks => Set<PaymentLink>();
    public DbSet<FiscalInvoice> FiscalInvoices => Set<FiscalInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RepairShopDbContext).Assembly);

        // Domain entities generate their own Guid ids: never let EF treat a set key as "existing".
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var id = entity.FindProperty("Id");
            if (id is not null && id.ClrType == typeof(Guid) && entity.FindPrimaryKey()?.Properties.Contains(id) == true)
                id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }

        // Defense in depth: every shop-scoped entity is filtered by the current shop (when there is one).
        var apply = typeof(RepairShopDbContext).GetMethod(nameof(ApplyShopFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => typeof(IShopScoped).IsAssignableFrom(e.ClrType) && e.BaseType is null))
        {
            apply.MakeGenericMethod(entity.ClrType).Invoke(this, new object[] { modelBuilder });
        }
    }

    private void ApplyShopFilter<T>(ModelBuilder modelBuilder) where T : class, IShopScoped
        => modelBuilder.Entity<T>().HasQueryFilter(e => CurrentShopId == null || e.ShopId == CurrentShopId);
}
