using Microsoft.EntityFrameworkCore;
using RepairShop.Domain.Saas;

namespace RepairShop.Infrastructure.Persistence;

public sealed partial class RepairShopDbContext
{
    public DbSet<ShopSubscription> ShopSubscriptions => Set<ShopSubscription>();
    public DbSet<BillingEvent> BillingEvents => Set<BillingEvent>();
    public DbSet<ShopProfile> ShopProfiles => Set<ShopProfile>();
    public DbSet<FiscalSettings> FiscalSettings => Set<FiscalSettings>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CounterSale> CounterSales => Set<CounterSale>();
    public DbSet<AccountEntry> AccountEntries => Set<AccountEntry>();
    public DbSet<FiscalInvoice> FiscalInvoices => Set<FiscalInvoice>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<OrderTracking> OrderTrackings => Set<OrderTracking>();
    public DbSet<PortalSignature> PortalSignatures => Set<PortalSignature>();
    public DbSet<SatisfactionSurvey> SatisfactionSurveys => Set<SatisfactionSurvey>();
    public DbSet<OrderAssignment> OrderAssignments => Set<OrderAssignment>();
    public DbSet<OrderDiagram> OrderDiagrams => Set<OrderDiagram>();
    public DbSet<ServiceCatalogItem> ServiceCatalog => Set<ServiceCatalogItem>();
    public DbSet<ShopAlert> ShopAlerts => Set<ShopAlert>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<IntegrationSettings> IntegrationSettings => Set<IntegrationSettings>();

    private static void ConfigureSaas(ModelBuilder m)
    {
        Configure<ShopSubscription>(m, "saas_subscription");
        Configure<BillingEvent>(m, "saas_billing_event");
        Configure<ShopProfile>(m, "saas_shop_profile");
        Configure<FiscalSettings>(m, "saas_fiscal_settings");
        Configure<CustomerContact>(m, "saas_customer_contact");
        Configure<CashSession>(m, "saas_cash_session");
        Configure<CashMovement>(m, "saas_cash_movement");
        Configure<CounterSale>(m, "saas_counter_sale");
        Configure<AccountEntry>(m, "saas_account_entry");
        Configure<FiscalInvoice>(m, "saas_fiscal_invoice");
        Configure<Appointment>(m, "saas_appointment");
        Configure<OrderTracking>(m, "saas_order_tracking");
        Configure<PortalSignature>(m, "saas_portal_signature");
        Configure<SatisfactionSurvey>(m, "saas_survey");
        Configure<OrderAssignment>(m, "saas_order_assignment");
        Configure<OrderDiagram>(m, "saas_order_diagram");
        Configure<ServiceCatalogItem>(m, "saas_service_catalog");
        Configure<ShopAlert>(m, "saas_alert");
        Configure<ApiKey>(m, "saas_api_key");
        Configure<WebhookEndpoint>(m, "saas_webhook_endpoint");
        Configure<WebhookDelivery>(m, "saas_webhook_delivery");
        Configure<IntegrationSettings>(m, "saas_integration_settings");

        // Large payloads: logo, drawn signatures and PEM material.
        m.Entity<ShopProfile>().Property(x => x.LogoDataUrl).HasMaxLength(400_000);
        m.Entity<PortalSignature>().Property(x => x.SignatureDataUrl).HasMaxLength(400_000);
        m.Entity<FiscalSettings>().Property(x => x.ProtectedCertificate).HasMaxLength(40_000);
        m.Entity<FiscalSettings>().Property(x => x.ProtectedPrivateKey).HasMaxLength(40_000);

        m.Entity<ShopSubscription>().HasIndex(x => x.ShopId).IsUnique().HasDatabaseName("IX_saas_subscription_ShopId_unique");
        m.Entity<ShopSubscription>().HasIndex(x => x.ProviderSubscriptionId);
        m.Entity<ShopProfile>().HasIndex(x => x.ShopId).IsUnique().HasDatabaseName("IX_saas_shop_profile_ShopId_unique");
        m.Entity<ShopProfile>().HasIndex(x => x.Slug).IsUnique();
        m.Entity<FiscalSettings>().HasIndex(x => x.ShopId).IsUnique().HasDatabaseName("IX_saas_fiscal_settings_ShopId_unique");
        m.Entity<IntegrationSettings>().HasIndex(x => x.ShopId).IsUnique().HasDatabaseName("IX_saas_integration_settings_ShopId_unique");
        m.Entity<CustomerContact>().HasIndex(x => new { x.ShopId, x.CustomerId }).IsUnique();
        m.Entity<CounterSale>().HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        m.Entity<CashSession>().HasIndex(x => new { x.ShopId, x.BranchId }).IsUnique().HasFilter("\"Status\" = 'Open'");
        m.Entity<CashMovement>().HasIndex(x => new { x.ShopId, x.SessionId });
        m.Entity<AccountEntry>().HasIndex(x => new { x.ShopId, x.CustomerId });
        m.Entity<FiscalInvoice>().HasIndex(x => new { x.ShopId, x.SourceType, x.SourceId });
        m.Entity<FiscalInvoice>().HasIndex(x => new { x.ShopId, x.PointOfSale, x.VoucherType, x.Number }).IsUnique().HasFilter("\"Status\" = 'Authorized'");
        m.Entity<Appointment>().HasIndex(x => new { x.ShopId, x.StartsAtUtc });
        m.Entity<OrderTracking>().HasIndex(x => new { x.ShopId, x.OrderId }).IsUnique();
        m.Entity<OrderTracking>().HasIndex(x => x.Code).IsUnique();
        m.Entity<SatisfactionSurvey>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<SatisfactionSurvey>().HasIndex(x => new { x.ShopId, x.OrderId }).IsUnique();
        m.Entity<OrderAssignment>().HasIndex(x => new { x.ShopId, x.OrderId }).IsUnique();
        m.Entity<OrderAssignment>().HasIndex(x => new { x.ShopId, x.TechnicianId });
        m.Entity<OrderDiagram>().HasIndex(x => new { x.ShopId, x.OrderId }).IsUnique();
        m.Entity<ServiceCatalogItem>().HasIndex(x => new { x.ShopId, x.Code }).IsUnique();
        m.Entity<ShopAlert>().HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
        m.Entity<ApiKey>().HasIndex(x => x.Hash).IsUnique();
        m.Entity<WebhookDelivery>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });

        m.Entity<CashMovement>().HasOne<CashSession>().WithMany().HasForeignKey(x => new { x.ShopId, x.SessionId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CounterSale>().HasOne<CashSession>().WithMany().HasForeignKey(x => new { x.ShopId, x.SessionId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WebhookDelivery>().HasOne<WebhookEndpoint>().WithMany().HasForeignKey(x => new { x.ShopId, x.EndpointId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
