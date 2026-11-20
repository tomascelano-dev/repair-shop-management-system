using Microsoft.Extensions.Logging;
using RepairShop.Application.Billing;
using RepairShop.Domain.Notifications;

namespace RepairShop.Infrastructure.Notifications;

/// <summary>
/// Sends a platform email right away through the configured email sender (SMTP), outside the shops' outbox:
/// these messages belong to no shop and must not show up in a shop's message history.
/// </summary>
public sealed class PlatformMailer : IPlatformMailer
{
    private readonly NotificationRouter _router;
    private readonly ILogger<PlatformMailer> _logger;

    public PlatformMailer(NotificationRouter router, ILogger<PlatformMailer> logger)
    {
        _router = router;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var sender = _router.Resolve(NotificationChannel.Email);
        if (sender is null)
        {
            _logger.LogWarning("Platform email \"{Subject}\" not sent: email is not configured.", subject);
            return false;
        }
        try
        {
            // Transient item: never saved, it only carries recipient, subject and body to the sender.
            var item = new NotificationOutboxItem(Guid.Empty, NotificationChannel.Email, to, subject, body, OutboxStatus.Pending, null, "platform", null, DateTime.UtcNow);
            var result = await sender.SendAsync(item, ct);
            if (!result.Success) _logger.LogError("Platform email \"{Subject}\" failed: {Error}", subject, result.Error);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform email \"{Subject}\" failed.", subject);
            return false;
        }
    }
}
