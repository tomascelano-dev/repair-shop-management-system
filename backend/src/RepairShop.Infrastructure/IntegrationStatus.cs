using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Notifications;
using RepairShop.Infrastructure.Files;
using RepairShop.Infrastructure.Notifications;

namespace RepairShop.Infrastructure;

public sealed class IntegrationStatus : IIntegrationStatus
{
    private readonly IIntegrationStatusChannels _channels;
    private readonly IAiAssistant _ai;
    private readonly StorageOptions _storage;

    public IntegrationStatus(IIntegrationStatusChannels channels, IAiAssistant ai, IOptions<StorageOptions> storage)
    {
        _channels = channels;
        _ai = ai;
        _storage = storage.Value;
    }

    public bool IsChannelConfigured(NotificationChannel channel) => _channels.IsConfigured(channel);

    public string StorageProvider => string.Equals(_storage.Provider, "S3", StringComparison.OrdinalIgnoreCase) ? "S3" : "Local";

    public bool AiConfigured => _ai.IsConfigured;
}
