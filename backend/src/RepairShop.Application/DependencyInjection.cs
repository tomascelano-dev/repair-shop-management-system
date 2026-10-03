using Microsoft.Extensions.DependencyInjection;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Admin;
using RepairShop.Application.Cash;
using RepairShop.Application.Currency;
using RepairShop.Application.Customers;
using RepairShop.Application.Documents;
using RepairShop.Application.Files;
using RepairShop.Application.Fiscal;
using RepairShop.Application.Imports;
using RepairShop.Application.Inventory;
using RepairShop.Application.Jobs;
using RepairShop.Application.Notifications;
using RepairShop.Application.Payments;
using RepairShop.Application.Portal;
using RepairShop.Application.Quotes;
using RepairShop.Application.RepairOrders;
using RepairShop.Application.Reports;
using RepairShop.Application.Sales;
using RepairShop.Application.Security;
using RepairShop.Application.Suggestions;

namespace RepairShop.Application;

public static class DependencyInjection
{
    /// <summary>Use cases (all scoped: they share the request's unit of work).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuditLog, AuditLog>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<ShopSettingsService>();

        services.AddScoped<CustomerService>();
        services.AddScoped<DeviceService>();
        services.AddScoped<ImportService>();

        services.AddScoped<RenderOrderMessageService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<RepairOrderService>();
        services.AddScoped<OrderRecordsService>();
        services.AddScoped<ChangeOrderStatusService>();
        services.AddScoped<QuoteService>();
        services.AddScoped<SuggestionService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<FileService>();

        services.AddScoped<CashRegisterService>();
        services.AddScoped<OrderPaymentService>();
        services.AddScoped<PaymentLinkService>();
        services.AddScoped<SalesService>();
        services.AddScoped<FiscalService>();
        services.AddScoped<ExchangeRateService>();

        services.AddScoped<InventoryService>();
        services.AddScoped<PurchasingService>();
        services.AddScoped<StockTransferService>();

        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();

        services.AddScoped<PublicPortalService>();
        services.AddScoped<ReminderService>();

        return services;
    }
}
