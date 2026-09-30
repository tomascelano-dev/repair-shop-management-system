using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Infrastructure.Persistence;
using RepairShop.Infrastructure.Repositories;
using RepairShop.Infrastructure.Security;
using RepairShop.Infrastructure.Time;

namespace RepairShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("RepairShopDb");
        services.AddDbContext<RepairShopDbContext>(opt => opt.UseNpgsql(cs));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Core
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // CRM
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();

        // Orders
        services.AddScoped<IRepairOrderRepository, RepairOrderRepository>();
        services.AddScoped<IRepairOrderStatusHistoryRepository, RepairOrderStatusHistoryRepository>();
        services.AddScoped<IRepairOrderNoteRepository, RepairOrderNoteRepository>();
        services.AddScoped<IRepairOrderAttachmentRepository, RepairOrderAttachmentRepository>();
        services.AddScoped<IRepairOrderPaymentRepository, RepairOrderPaymentRepository>();
        services.AddScoped<IRepairOrderReceptionChecklistRepository, RepairOrderReceptionChecklistRepository>();

        // Messaging / Audit
        services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();

        // Inventory
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IInventoryAdjustmentRepository, InventoryAdjustmentRepository>();
        services.AddScoped<IRepairOrderPartUsageRepository, RepairOrderPartUsageRepository>();

        return services;
    }
}
