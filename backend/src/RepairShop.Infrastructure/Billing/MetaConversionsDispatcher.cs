using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Billing;
using RepairShop.Domain.Billing;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Billing;

/// <summary>Sends the queued conversions to the Meta Conversions API (POST /{pixel}/events).</summary>
public sealed class MetaConversionsDispatcher
{
    public const string HttpClientName = "meta-conversions";

    private readonly RepairShopDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IDateTimeProvider _clock;
    private readonly TrackingOptions _options;
    private readonly ILogger<MetaConversionsDispatcher> _logger;

    public MetaConversionsDispatcher(RepairShopDbContext db, IHttpClientFactory http, IDateTimeProvider clock, IOptions<TrackingOptions> options, ILogger<MetaConversionsDispatcher> logger)
    {
        _db = db;
        _http = http;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Sends one batch of due events. Returns how many were handled.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        if (!_options.MetaServerEventsEnabled) return 0;
        var now = _clock.UtcNow;
        var due = await _db.AdConversions
            .Where(x => x.Platform == AdConversionService.Meta && x.Status == AdConversionStatus.Pending && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        // Meta only accepts events from the last 7 days.
        foreach (var stale in due.Where(x => x.CreatedAtUtc < now.AddDays(-6)))
            stale.RegisterFailure("Expired before it could be sent.", permanent: true, now);
        // Recheck consent before delivery: an owner may have withdrawn it after these events were queued.
        var organizations = due.Select(x => x.OrganizationId).Distinct().ToArray();
        var consented = await _db.SignupAttributions.AsNoTracking()
            .Where(x => organizations.Contains(x.OrganizationId) && x.AdConsent)
            .Select(x => x.OrganizationId).ToListAsync(ct);
        foreach (var revoked in due.Where(x => x.Status == AdConversionStatus.Pending && !consented.Contains(x.OrganizationId)))
            revoked.RegisterFailure("Advertising consent withdrawn.", permanent: true, now);
        var batch = due.Where(x => x.Status == AdConversionStatus.Pending).ToList();

        if (batch.Count > 0)
        {
            var body = new JsonObject
            {
                ["data"] = new JsonArray(batch.Select(x => JsonNode.Parse(x.Payload)).ToArray()),
                ["access_token"] = _options.MetaCapiToken.Trim(),
            };
            if (!string.IsNullOrWhiteSpace(_options.MetaTestEventCode)) body["test_event_code"] = _options.MetaTestEventCode.Trim();

            var url = $"{_options.MetaApiBaseUrl.TrimEnd('/')}/{_options.MetaApiVersion}/{TrackingOptions.PublicId(_options.MetaPixelId)}/events";
            try
            {
                using var res = await _http.CreateClient(HttpClientName).PostAsJsonAsync(url, body, ct);
                var text = await res.Content.ReadAsStringAsync(ct);
                if (res.IsSuccessStatusCode)
                {
                    foreach (var x in batch) x.MarkSent(_clock.UtcNow);
                }
                else
                {
                    // 4xx: Meta rejected the data or the token (retrying won't help); 5xx and 429: try again later.
                    var permanent = (int)res.StatusCode is >= 400 and < 500 and not 429;
                    var error = $"{(int)res.StatusCode}: {ErrorOf(text)}";
                    _logger.LogWarning("Meta Conversions API rejected {Count} events: {Error}", batch.Count, error);
                    foreach (var x in batch) x.RegisterFailure(error, permanent, _clock.UtcNow);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not reach the Meta Conversions API.");
                foreach (var x in batch) x.RegisterFailure(ex.Message, permanent: false, _clock.UtcNow);
            }
        }

        await _db.SaveChangesAsync(ct);
        return due.Count;
    }

    private static string ErrorOf(string body)
    {
        try
        {
            var error = JsonNode.Parse(body)?["error"];
            var message = error?["error_user_msg"]?.GetValue<string>() ?? error?["message"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(message)) return message;
        }
        catch (JsonException) { }
        return body.Length > 300 ? body[..300] : body;
    }
}

public sealed class MetaConversionsDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TrackingOptions _options;
    private readonly ILogger<MetaConversionsDispatcherService> _logger;

    public MetaConversionsDispatcherService(IServiceScopeFactory scopes, IOptions<TrackingOptions> options, ILogger<MetaConversionsDispatcherService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.MetaServerEventsEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<MetaConversionsDispatcher>().RunOnceAsync(stoppingToken) > 0) continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Meta conversions dispatcher iteration failed.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
