using RepairShop.Domain.Notifications;

namespace RepairShop.Application.Abstractions;

/// <summary>Which global (environment-level) integrations are configured. Never exposes secrets.</summary>
public interface IIntegrationStatus
{
    bool IsChannelConfigured(NotificationChannel channel);
    string StorageProvider { get; }
    bool AiConfigured { get; }
}
