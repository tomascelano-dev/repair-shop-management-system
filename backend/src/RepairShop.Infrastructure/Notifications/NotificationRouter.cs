using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Notifications;

namespace RepairShop.Infrastructure.Notifications;

/// <summary>Chooses the configured sender for each channel.</summary>
public sealed class NotificationRouter : IIntegrationStatusChannels
{
    private readonly IServiceProvider _sp;
    private readonly NotificationOptions _opt;

    public NotificationRouter(IServiceProvider sp, IOptions<NotificationOptions> options)
    {
        _sp = sp;
        _opt = options.Value;
    }

    public INotificationSender? Resolve(NotificationChannel channel)
    {
        var provider = ProviderFor(channel).ToLowerInvariant();
        return (channel, provider) switch
        {
            (_, "simulated") => _sp.GetRequiredService<SimulatedNotificationSender>(),
            (NotificationChannel.WhatsApp, "twilio") or (NotificationChannel.Sms, "twilio") => _sp.GetRequiredService<TwilioNotificationSender>(),
            (NotificationChannel.WhatsApp, "meta") => _sp.GetRequiredService<MetaWhatsAppSender>(),
            (NotificationChannel.Email, "smtp") => _sp.GetRequiredService<SmtpEmailSender>(),
            _ => null
        };
    }

    public bool IsConfigured(NotificationChannel channel) => !string.Equals(ProviderFor(channel), "None", StringComparison.OrdinalIgnoreCase);

    private string ProviderFor(NotificationChannel channel) => channel switch
    {
        NotificationChannel.WhatsApp => _opt.WhatsApp.Provider,
        NotificationChannel.Sms => _opt.Sms.Provider,
        NotificationChannel.Email => _opt.Email.Provider,
        _ => "None"
    };
}

public interface IIntegrationStatusChannels
{
    bool IsConfigured(NotificationChannel channel);
}
