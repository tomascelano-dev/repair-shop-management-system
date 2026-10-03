using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Notifications;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Notifications;

/// <summary>
/// Sends queued messages. Claims due items with FOR UPDATE SKIP LOCKED so several API instances can run
/// the dispatcher concurrently without sending duplicates. Failures are retried with backoff.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly RepairShopDbContext _db;
    private readonly NotificationRouter _router;
    private readonly IDateTimeProvider _clock;
    private readonly NotificationOptions _opt;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(RepairShopDbContext db, NotificationRouter router, IDateTimeProvider clock, IOptions<NotificationOptions> options, ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _router = router;
        _clock = clock;
        _opt = options.Value;
        _logger = logger;
    }

    /// <summary>Processes one batch. Returns the number of messages handled.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // Recover items stuck in "Processing" (e.g. the process died mid-send).
        await _db.NotificationOutbox.IgnoreQueryFilters()
            .Where(x => x.Status == OutboxStatus.Processing && x.UpdatedAtUtc < now.AddMinutes(-10))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OutboxStatus.Failed).SetProperty(x => x.NextAttemptAtUtc, now), ct);

        var ids = await _db.Database.SqlQuery<Guid>($"""
            UPDATE notification_outbox SET "Status" = {(int)OutboxStatus.Processing}, "UpdatedAtUtc" = {now}
            WHERE "Id" IN (
                SELECT "Id" FROM notification_outbox
                WHERE "Status" IN ({(int)OutboxStatus.Pending}, {(int)OutboxStatus.Failed})
                  AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now})
                ORDER BY "CreatedAtUtc"
                LIMIT {Math.Clamp(_opt.BatchSize, 1, 200)}
                FOR UPDATE SKIP LOCKED)
            RETURNING "Id" AS "Value"
            """).ToListAsync(ct);

        if (ids.Count == 0) return 0;

        var items = await _db.NotificationOutbox.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        foreach (var item in items)
        {
            var sender = _router.Resolve(item.Channel);
            if (sender is null)
            {
                item.RegisterFailure($"No hay un proveedor configurado para {item.Channel}. Envialo manualmente desde la orden.", permanent: true, _clock.UtcNow);
                continue;
            }

            try
            {
                var result = await sender.SendAsync(item, ct);
                if (result.Success) item.MarkSent(result.Provider, result.ProviderMessageId, _clock.UtcNow);
                else item.RegisterFailure(result.Error ?? "Error desconocido", result.Permanent, _clock.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Sending notification {Id} via {Sender} failed.", item.Id, sender.Name);
                item.RegisterFailure(ex.Message, permanent: false, _clock.UtcNow);
            }
        }

        await _db.SaveChangesAsync(ct);
        return items.Count;
    }
}

public sealed class NotificationDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly NotificationOptions _opt;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(IServiceScopeFactory scopes, IOptions<NotificationOptions> options, ILogger<NotificationDispatcherService> logger)
    {
        _scopes = scopes;
        _opt = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.DispatcherEnabled) return;
        var delay = TimeSpan.FromSeconds(Math.Clamp(_opt.PollSeconds, 2, 300));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                var handled = await dispatcher.RunOnceAsync(stoppingToken);
                if (handled > 0) continue; // drain the queue quickly
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Notification dispatcher iteration failed.");
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
