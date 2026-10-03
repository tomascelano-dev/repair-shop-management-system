using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Currency;
using RepairShop.Application.Jobs;
using RepairShop.Application.Quotes;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Jobs;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    public bool Enabled { get; set; } = true;
    public int RemindersIntervalMinutes { get; set; } = 60;
    public int QuoteExpiryIntervalMinutes { get; set; } = 30;
    public int ExchangeRatesIntervalMinutes { get; set; } = 360;
    public int CleanupIntervalMinutes { get; set; } = 720;
}

/// <summary>
/// Periodic work: pickup reminders, satisfaction surveys, quote expiry, exchange rates and cleanup.
/// Every job is idempotent, so running several API instances is safe (each job additionally takes a
/// PostgreSQL advisory lock to avoid duplicated effort).
/// </summary>
public sealed class ScheduledJobsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly JobsOptions _opt;
    private readonly ILogger<ScheduledJobsService> _logger;
    private readonly Dictionary<string, DateTime> _lastRun = new();

    public ScheduledJobsService(IServiceScopeFactory scopes, IOptions<JobsOptions> options, ILogger<ScheduledJobsService> logger)
    {
        _scopes = scopes;
        _opt = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled) return;

        // Let the app finish starting (migrations, seeding) before the first run.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIfDueAsync("quotes.expire", _opt.QuoteExpiryIntervalMinutes, sp => sp.GetRequiredService<QuoteService>().ExpireDueAsync(stoppingToken), stoppingToken);
            await RunIfDueAsync("orders.ready_reminders", _opt.RemindersIntervalMinutes, sp => sp.GetRequiredService<ReminderService>().SendReadyRemindersAsync(stoppingToken), stoppingToken);
            await RunIfDueAsync("orders.feedback_surveys", _opt.RemindersIntervalMinutes, sp => sp.GetRequiredService<ReminderService>().SendFeedbackSurveysAsync(stoppingToken), stoppingToken);
            await RunIfDueAsync("currency.refresh", _opt.ExchangeRatesIntervalMinutes, sp => sp.GetRequiredService<ExchangeRateService>().RefreshFromProviderAsync(stoppingToken), stoppingToken);
            await RunIfDueAsync("maintenance.cleanup", _opt.CleanupIntervalMinutes, sp => CleanupAsync(sp, stoppingToken), stoppingToken);

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunIfDueAsync(string name, int intervalMinutes, Func<IServiceProvider, Task<int>> job, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (_lastRun.TryGetValue(name, out var last) && now - last < TimeSpan.FromMinutes(Math.Max(1, intervalMinutes))) return;
        _lastRun[name] = now;

        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
            await using var lockHandle = await AdvisoryLock.TryAcquireAsync(db, name, ct);
            if (lockHandle is null) return; // another instance is running it

            var affected = await job(scope.ServiceProvider);
            if (affected > 0) _logger.LogInformation("Job {Job} processed {Count} item(s).", name, affected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Job {Job} failed.", name);
        }
    }

    internal static async Task<int> CleanupAsync(IServiceProvider sp, CancellationToken ct)
    {
        var clock = sp.GetRequiredService<IDateTimeProvider>();
        var db = sp.GetRequiredService<RepairShopDbContext>();
        var now = clock.UtcNow;

        var tokens = await sp.GetRequiredService<IRefreshTokenRepository>().DeleteExpiredAsync(now.AddDays(-7), ct);
        var idempotency = await db.IdempotencyRecords.Where(x => x.ExpiresAtUtc < now).ExecuteDeleteAsync(ct);
        return tokens + idempotency;
    }
}

/// <summary>Session-level PostgreSQL advisory lock; released on dispose.</summary>
internal sealed class AdvisoryLock : IAsyncDisposable
{
    private readonly RepairShopDbContext _db;
    private readonly long _key;

    private AdvisoryLock(RepairShopDbContext db, long key)
    {
        _db = db;
        _key = key;
    }

    public static async Task<AdvisoryLock?> TryAcquireAsync(RepairShopDbContext db, string name, CancellationToken ct)
    {
        var key = StableHash(name);
        await db.Database.OpenConnectionAsync(ct);
        var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({key}) AS \"Value\"").SingleAsync(ct);
        if (acquired) return new AdvisoryLock(db, key);

        await db.Database.CloseConnectionAsync();
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _db.Database.SqlQuery<bool>($"SELECT pg_advisory_unlock({_key}) AS \"Value\"").SingleAsync();
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    private static long StableHash(string value)
    {
        // FNV-1a 64-bit: stable across processes (string.GetHashCode is randomized).
        unchecked
        {
            var hash = (long)14695981039346656037UL;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 1099511628211L;
            }
            return hash;
        }
    }
}
