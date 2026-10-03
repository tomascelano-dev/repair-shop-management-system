namespace RepairShop.Domain.Billing;

/// <summary>
/// Where a self-service signup came from (ad campaign parameters and click ids captured by the website) and
/// whether the visitor accepted advertising cookies, which decides if conversions are reported to the ad platforms.
/// </summary>
public sealed class SignupAttribution
{
    public Guid OrganizationId { get; private set; }

    /// <summary>Owner who signed up (their email identifies the conversion for the ad platforms).</summary>
    public Guid UserId { get; private set; }

    public string? Source { get; private set; }
    public string? Medium { get; private set; }
    public string? Campaign { get; private set; }
    public string? Term { get; private set; }
    public string? Content { get; private set; }

    /// <summary>Google Ads click ids (gclid; gbraid and wbraid on iOS).</summary>
    public string? Gclid { get; private set; }
    public string? Gbraid { get; private set; }
    public string? Wbraid { get; private set; }

    /// <summary>Meta click id and the Meta pixel cookies (_fbp browser id, _fbc click id).</summary>
    public string? Fbclid { get; private set; }
    public string? Fbp { get; private set; }
    public string? Fbc { get; private set; }

    /// <summary>First page of the visit and the external page that sent the visitor.</summary>
    public string? LandingPath { get; private set; }
    public string? Referrer { get; private set; }

    /// <summary>The visitor accepted advertising cookies (or was not asked to, outside the EU) when signing up.</summary>
    public bool AdConsent { get; private set; }

    /// <summary>Id the website used for the trial conversion, so the server-side copy is deduplicated.</summary>
    public string? TrialEventId { get; private set; }

    public string? ClientIp { get; private set; }
    public string? UserAgent { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private SignupAttribution() { } // EF

    public SignupAttribution(Guid organizationId, Guid userId, SignupAttributionData data, string? clientIp, string? userAgent, DateTime nowUtc)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Source = Clean(data.Source, 120);
        Medium = Clean(data.Medium, 120);
        Campaign = Clean(data.Campaign, 200);
        Term = Clean(data.Term, 200);
        Content = Clean(data.Content, 200);
        Gclid = Clean(data.Gclid, 300);
        Gbraid = Clean(data.Gbraid, 300);
        Wbraid = Clean(data.Wbraid, 300);
        Fbclid = Clean(data.Fbclid, 300);
        Fbp = Clean(data.Fbp, 300);
        Fbc = Clean(data.Fbc, 400);
        LandingPath = Clean(data.LandingPath, 300);
        Referrer = Clean(data.Referrer, 500);
        AdConsent = data.AdConsent;
        TrialEventId = Clean(data.EventId, 64);
        ClientIp = Clean(clientIp, 64);
        UserAgent = Clean(userAgent, 300);
        CreatedAtUtc = nowUtc;
    }

    /// <summary>True when the signup carries any campaign or click information.</summary>
    public bool FromCampaign => Source is not null || Campaign is not null || Gclid is not null || Gbraid is not null || Wbraid is not null || Fbclid is not null;

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = new string(value.Trim().Where(c => !char.IsControl(c)).ToArray());
        if (clean.Length == 0) return null;
        return clean.Length > max ? clean[..max] : clean;
    }
}

/// <summary>What the website captured about the visit that ended in a signup.</summary>
public sealed record SignupAttributionData(
    string? Source = null,
    string? Medium = null,
    string? Campaign = null,
    string? Term = null,
    string? Content = null,
    string? Gclid = null,
    string? Gbraid = null,
    string? Wbraid = null,
    string? Fbclid = null,
    string? Fbp = null,
    string? Fbc = null,
    string? LandingPath = null,
    string? Referrer = null,
    bool AdConsent = false,
    string? EventId = null);
