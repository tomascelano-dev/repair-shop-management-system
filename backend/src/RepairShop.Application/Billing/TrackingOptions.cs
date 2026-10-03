using System.Text.RegularExpressions;

namespace RepairShop.Application.Billing;

/// <summary>
/// Advertising measurement for the public website (Google Analytics 4, Google Ads, Meta pixel) and the Meta
/// Conversions API. Every value is optional: what is empty is simply not loaded.
/// </summary>
public sealed partial class TrackingOptions
{
    public const string SectionName = "Tracking";

    /// <summary>Google Analytics 4 measurement id (G-XXXXXXX).</summary>
    public string Ga4Id { get; set; } = "";

    /// <summary>Google Ads tag id (AW-123456789).</summary>
    public string GoogleAdsId { get; set; } = "";

    /// <summary>Label of the Google Ads conversion for a started trial (the part after the slash in send_to).</summary>
    public string GoogleAdsSignupLabel { get; set; } = "";

    /// <summary>Label of the Google Ads conversion for a paid subscription.</summary>
    public string GoogleAdsPurchaseLabel { get; set; } = "";

    /// <summary>Meta pixel (dataset) id.</summary>
    public string MetaPixelId { get; set; } = "";

    /// <summary>Access token of the Meta Conversions API (secret).</summary>
    public string MetaCapiToken { get; set; } = "";

    /// <summary>Test event code from Meta Events Manager → Test events, while checking the setup. Leave empty in production.</summary>
    public string MetaTestEventCode { get; set; } = "";

    public string MetaApiBaseUrl { get; set; } = "https://graph.facebook.com/";
    public string MetaApiVersion { get; set; } = "v21.0";

    /// <summary>Conversions are also sent from the server when the pixel and the Conversions API token are set.</summary>
    public bool MetaServerEventsEnabled => PublicId(MetaPixelId) is not null && !string.IsNullOrWhiteSpace(MetaCapiToken);

    /// <summary>
    /// The value when it looks like a tag id or conversion label (letters, digits, dashes and underscores); null
    /// otherwise, so a typo in the .env never ends up inside a script URL.
    /// </summary>
    public static string? PublicId(string? value)
    {
        var v = value?.Trim();
        return !string.IsNullOrEmpty(v) && TagId().IsMatch(v) ? v : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex TagId();
}
