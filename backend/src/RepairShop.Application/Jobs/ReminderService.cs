using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Notifications;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Jobs;

/// <summary>
/// Scheduled customer messages. Idempotent thanks to deterministic correlation keys, so it is safe
/// to run often and on several instances at the same time.
/// </summary>
public sealed class ReminderService
{
    private readonly IRepairOrderRepository _orders;
    private readonly IShopRepository _shops;
    private readonly ICustomerFeedbackRepository _feedback;
    private readonly NotificationService _notifications;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(
        IRepairOrderRepository orders,
        IShopRepository shops,
        ICustomerFeedbackRepository feedback,
        NotificationService notifications,
        IDateTimeProvider clock,
        ILogger<ReminderService> logger)
    {
        _orders = orders;
        _shops = shops;
        _feedback = feedback;
        _notifications = notifications;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>"Your device is ready" reminders after N days (shop setting, e.g. 7,15,30).</summary>
    public async Task<int> SendReadyRemindersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var candidates = await _orders.ListForJobsAsync(RepairOrderStatus.Ready, now.AddDays(-1), null, null, 500, ct);
        var sent = 0;

        foreach (var order in candidates)
        {
            var shop = await _shops.GetByIdAsync(order.ShopId, ct);
            if (shop is null || !shop.NotificationsEnabled || order.ReadyAtUtc is null) continue;

            // Only the most recent reminder that is due (never several at once).
            var due = shop.GetReadyReminderDays().Where(d => order.ReadyAtUtc.Value.AddDays(d) <= now).DefaultIfEmpty(0).Max();
            if (due == 0) continue;

            try
            {
                var result = await _notifications.NotifyOrderAsync(order.ShopId, order, TemplateKeys.ReadyReminder,
                    $"order:{order.Id}:ready_reminder:{order.ReadyAtUtc:yyyyMMdd}:{due}", true, null, Actor.System, ct);
                if (result.OutboxItemId is not null) sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ready reminder failed for order {OrderId}.", order.Id);
            }
        }

        return sent;
    }

    /// <summary>Satisfaction survey a few hours after delivery.</summary>
    public async Task<int> SendFeedbackSurveysAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var candidates = await _orders.ListForJobsAsync(RepairOrderStatus.Delivered, null, now.AddDays(-7), now.AddHours(-2), 500, ct);
        var sent = 0;

        foreach (var order in candidates)
        {
            var shop = await _shops.GetByIdAsync(order.ShopId, ct);
            if (shop is null || !shop.NotificationsEnabled || !shop.SendFeedbackSurvey || order.IsWarrantyClaim) continue;
            if (await _feedback.GetByOrderAsync(order.ShopId, order.Id, ct) is not null) continue;

            try
            {
                var result = await _notifications.NotifyOrderAsync(order.ShopId, order, TemplateKeys.FeedbackRequest,
                    $"order:{order.Id}:survey", true, null, Actor.System, ct);
                if (result.OutboxItemId is not null) sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feedback survey failed for order {OrderId}.", order.Id);
            }
        }

        return sent;
    }
}
