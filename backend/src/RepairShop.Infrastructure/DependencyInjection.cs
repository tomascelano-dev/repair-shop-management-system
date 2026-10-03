using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Customers;
using RepairShop.Application.Security;
using RepairShop.Application.Billing;
using RepairShop.Infrastructure.Ai;
using RepairShop.Infrastructure.Billing;
using RepairShop.Infrastructure.Documents;
using RepairShop.Infrastructure.Files;
using RepairShop.Infrastructure.Idempotency;
using RepairShop.Infrastructure.Integrations;
using RepairShop.Infrastructure.Jobs;
using RepairShop.Infrastructure.Notifications;
using RepairShop.Infrastructure.Persistence;
using RepairShop.Infrastructure.Queries;
using RepairShop.Infrastructure.Repositories;
using RepairShop.Infrastructure.Security;
using RepairShop.Infrastructure.Spreadsheets;
using RepairShop.Infrastructure.Time;

namespace RepairShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, bool enableBackgroundWork = true)
    {
        var cs = config.GetConnectionString("RepairShopDb");
        // No retrying execution strategy: the unit of work uses explicit transactions (sales, payments, stock).
        services.AddDbContext<RepairShopDbContext>(opt => opt.UseNpgsql(cs));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddMemoryCache();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICounterService, CounterService>();
        services.AddScoped<IdempotencyStore>();

        // Secrets at rest (shop integration tokens, certificates): keys persisted in the database so
        // every API instance (and restarts) can decrypt them.
        services.AddDataProtection()
            .SetApplicationName("RepairShop")
            .PersistKeysToDbContext<RepairShopDbContext>();
        var masterKey = MasterKey.From(config);
        if (masterKey is not null)
            services.Configure<KeyManagementOptions>(o => o.XmlEncryptor = new MasterKeyXmlEncryptor(masterKey));
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        AddRepositories(services);
        AddQueries(services);
        AddFiles(services, config);
        AddIntegrations(services, config);
        AddBilling(services, config);
        AddNotifications(services, config);
        AddAi(services, config);

        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
        services.AddSingleton<ISpreadsheetReader, SpreadsheetReader>();
        services.AddSingleton<IIntegrationStatus, IntegrationStatus>();

        services.Configure<JobsOptions>(config.GetSection(JobsOptions.SectionName));
        if (enableBackgroundWork)
        {
            services.AddHostedService<NotificationDispatcherService>();
            services.AddHostedService<ScheduledJobsService>();
            services.AddHostedService<MetaConversionsDispatcherService>();
        }

        return services;
    }

    private static void AddRepositories(IServiceCollection services)
    {
        // Core
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IShopIntegrationRepository, ShopIntegrationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserShopAccessRepository, UserShopAccessRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IAdTrackingRepository, AdTrackingRepository>();
        services.AddScoped<IShopProvisioner, ShopProvisioner>();

        // CRM
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ICustomerFeedbackRepository, CustomerFeedbackRepository>();

        // Orders
        services.AddScoped<IRepairOrderRepository, RepairOrderRepository>();
        services.AddScoped<IRepairOrderStatusHistoryRepository, RepairOrderStatusHistoryRepository>();
        services.AddScoped<IRepairOrderNoteRepository, RepairOrderNoteRepository>();
        services.AddScoped<IRepairOrderAttachmentRepository, RepairOrderAttachmentRepository>();
        services.AddScoped<IRepairOrderPaymentRepository, RepairOrderPaymentRepository>();
        services.AddScoped<IRepairOrderReceptionChecklistRepository, RepairOrderReceptionChecklistRepository>();
        services.AddScoped<IRepairOrderQaChecklistRepository, RepairOrderQaChecklistRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();

        // Messaging / Audit
        services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();

        // Inventory / purchasing
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IInventoryAdjustmentRepository, InventoryAdjustmentRepository>();
        services.AddScoped<IRepairOrderPartUsageRepository, RepairOrderPartUsageRepository>();
        services.AddScoped<IInventoryReservationRepository, InventoryReservationRepository>();
        services.AddScoped<IInventoryCompatibilityRepository, InventoryCompatibilityRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();

        // Money
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<ICashMovementRepository, CashMovementRepository>();
        services.AddScoped<IPaymentLinkRepository, PaymentLinkRepository>();
        services.AddScoped<IFiscalInvoiceRepository, FiscalInvoiceRepository>();
    }

    private static void AddQueries(IServiceCollection services)
    {
        services.AddScoped<IRepairOrderReadModel, RepairOrderReadModel>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<ICustomerMergeStore, CustomerMergeStore>();
    }

    private static void AddFiles(IServiceCollection services, IConfiguration config)
    {
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));
        var provider = config.GetSection(StorageOptions.SectionName)["Provider"];
        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStorage, S3FileStorage>();
        else
            services.AddSingleton<IFileStorage, LocalFileStorage>();
    }

    private static void AddIntegrations(IServiceCollection services, IConfiguration config)
    {
        var userAgent = new ProductInfoHeaderValue("RepairShop", "1.0");

        services.AddHttpClient(MercadoPagoClient.HttpClientName, c =>
        {
            c.BaseAddress = new Uri(config["Integrations:MercadoPago:BaseUrl"] ?? "https://api.mercadopago.com/");
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.Add(userAgent);
        });
        services.AddSingleton<IMercadoPagoClient, MercadoPagoClient>();

        services.AddHttpClient(ArcaFiscalAuthority.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(40);
            c.DefaultRequestHeaders.UserAgent.Add(userAgent);
        });
        services.AddSingleton<IFiscalAuthority, ArcaFiscalAuthority>();

        services.AddHttpClient(DolarApiExchangeRateProvider.HttpClientName, c =>
        {
            c.BaseAddress = new Uri(config["Integrations:DolarApi:BaseUrl"] ?? "https://dolarapi.com/");
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.Add(userAgent);
        });
        if (string.Equals(config["ExchangeRates:Provider"] ?? "DolarApi", "None", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IExchangeRateProvider, NoExchangeRateProvider>();
        else
            services.AddSingleton<IExchangeRateProvider, DolarApiExchangeRateProvider>();
    }

    private static void AddBilling(IServiceCollection services, IConfiguration config)
    {
        services.Configure<BillingOptions>(config.GetSection(BillingOptions.SectionName));
        var userAgent = new ProductInfoHeaderValue("RepairShop", "1.0");

        services.AddHttpClient(MercadoPagoSubscriptionGateway.HttpClientName, c =>
        {
            c.BaseAddress = new Uri(config["Integrations:MercadoPago:BaseUrl"] ?? "https://api.mercadopago.com/");
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.Add(userAgent);
        });
        services.AddHttpClient(PaddleSubscriptionGateway.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.Add(userAgent);
        });
        services.AddSingleton<ISubscriptionGateway, MercadoPagoSubscriptionGateway>();
        services.AddSingleton<ISubscriptionGateway, PaddleSubscriptionGateway>();

        services.Configure<TrackingOptions>(config.GetSection(TrackingOptions.SectionName));
        services.AddHttpClient(MetaConversionsDispatcher.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.Add(userAgent);
        });
        services.AddScoped<MetaConversionsDispatcher>();
    }

    private static void AddNotifications(IServiceCollection services, IConfiguration config)
    {
        services.Configure<NotificationOptions>(config.GetSection(NotificationOptions.SectionName));

        services.AddHttpClient(TwilioNotificationSender.HttpClientName, (sp, c) =>
        {
            c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<NotificationOptions>>().Value.Twilio.BaseUrl);
            c.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient(MetaWhatsAppSender.HttpClientName, (sp, c) =>
        {
            c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<NotificationOptions>>().Value.Meta.BaseUrl);
            c.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddSingleton<SimulatedNotificationSender>();
        services.AddSingleton<TwilioNotificationSender>();
        services.AddSingleton<MetaWhatsAppSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<NotificationRouter>();
        services.AddSingleton<IIntegrationStatusChannels>(sp => sp.GetRequiredService<NotificationRouter>());
        services.AddScoped<NotificationDispatcher>();
    }

    private static void AddAi(IServiceCollection services, IConfiguration config)
    {
        services.Configure<AiOptions>(config.GetSection(AiOptions.SectionName));
        var provider = config.GetSection(AiOptions.SectionName)["Provider"];
        if (string.Equals(provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IAiAssistant, AnthropicAiAssistant>();
        else
            services.AddSingleton<IAiAssistant, NullAiAssistant>();
    }
}
