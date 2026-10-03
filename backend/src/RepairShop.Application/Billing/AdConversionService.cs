using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Billing;

/// <summary>
/// Queues the conversions the ad platforms optimize for (trial started, subscription paid) to be sent from the
/// server through the Meta Conversions API. Only for signups that accepted advertising cookies; the website sends
/// its own copy of each event with the same id, and Meta counts it once.
/// </summary>
public sealed class AdConversionService
{
    public const string Meta = "meta";
    public const string TrialStarted = "StartTrial";
    public const string Purchase = "Purchase";

    private readonly IAdTrackingRepository _tracking;
    private readonly IUserRepository _users;
    private readonly IAppLinks _links;
    private readonly IDateTimeProvider _clock;
    private readonly TrackingOptions _options;

    public AdConversionService(IAdTrackingRepository tracking, IUserRepository users, IAppLinks links, IDateTimeProvider clock, IOptions<TrackingOptions> options)
    {
        _tracking = tracking;
        _users = users;
        _links = links;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>Id of the purchase conversion of a paid subscription, shared by the website and the server.</summary>
    public static string? PurchaseEventId(Subscription sub)
        => sub.HasLiveProviderSubscription ? $"purchase_{sub.ProviderSubscriptionId}" : null;

    /// <summary>Queues the trial conversion of a new signup (same transaction as the signup).</summary>
    public async Task QueueTrialStartedAsync(SignupAttribution attribution, AppUser owner, string country, CancellationToken ct)
    {
        if (!_options.MetaServerEventsEnabled || !attribution.AdConsent || attribution.TrialEventId is null) return;
        var payload = MetaEvent(TrialStarted, attribution.TrialEventId, _links.Web("/registro"), attribution, owner.Email, country, 0m, "USD");
        await _tracking.AddConversionAsync(new AdConversion(attribution.OrganizationId, Meta, TrialStarted, attribution.TrialEventId, payload, _clock.UtcNow), ct);
    }

    /// <summary>Queues the purchase conversion when a subscription becomes paid for the first time.</summary>
    public async Task QueuePurchaseAsync(Subscription sub, CancellationToken ct)
    {
        if (!_options.MetaServerEventsEnabled || PurchaseEventId(sub) is not { } eventId) return;
        var attribution = await _tracking.GetAttributionAsync(sub.OrganizationId, ct);
        if (attribution is null || !attribution.AdConsent) return;
        if (await _tracking.ConversionExistsAsync(Meta, eventId, ct)) return;
        var owner = await _users.GetByIdAsync(attribution.UserId, ct);
        if (owner is null) return;

        var payload = MetaEvent(Purchase, eventId, _links.Billing(), attribution, owner.Email, sub.BillingCountry, sub.Amount ?? 0m, sub.Currency ?? "USD");
        await _tracking.AddConversionAsync(new AdConversion(sub.OrganizationId, Meta, Purchase, eventId, payload, _clock.UtcNow), ct);
    }

    /// <summary>
    /// One event of the Conversions API (https://developers.facebook.com/docs/marketing-api/conversions-api/parameters).
    /// Email, external id and country are sent hashed (SHA-256), as Meta requires.
    /// </summary>
    internal string MetaEvent(string name, string eventId, string sourceUrl, SignupAttribution a, string email, string country, decimal value, string currency)
    {
        var user = new JsonObject
        {
            ["em"] = new JsonArray(Sha256(email.Trim().ToLowerInvariant())),
            ["external_id"] = new JsonArray(Sha256(a.OrganizationId.ToString("N"))),
            ["country"] = new JsonArray(Sha256(country.Trim().ToLowerInvariant())),
        };
        if (a.ClientIp is not null) user["client_ip_address"] = a.ClientIp;
        if (a.UserAgent is not null) user["client_user_agent"] = a.UserAgent;
        if (a.Fbp is not null) user["fbp"] = a.Fbp;
        if ((a.Fbc ?? FbcFromClick(a)) is { } fbc) user["fbc"] = fbc;

        return new JsonObject
        {
            ["event_name"] = name,
            ["event_time"] = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds(),
            ["event_id"] = eventId,
            ["action_source"] = "website",
            ["event_source_url"] = sourceUrl,
            ["user_data"] = user,
            ["custom_data"] = new JsonObject
            {
                ["currency"] = currency.ToUpperInvariant(),
                ["value"] = decimal.Round(value, 2),
            },
        }.ToJsonString();
    }

    /// <summary>Meta's click id format (fb.1.time.fbclid) when the pixel cookie was not available.</summary>
    private static string? FbcFromClick(SignupAttribution a)
        => a.Fbclid is null ? null : $"fb.1.{new DateTimeOffset(a.CreatedAtUtc).ToUnixTimeMilliseconds()}.{a.Fbclid}";

    internal static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
