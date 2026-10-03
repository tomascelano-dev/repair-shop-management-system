namespace RepairShop.Domain.Billing;

public enum AdConversionStatus
{
    Pending = 1,
    Sent = 2,
    /// <summary>Rejected by the platform or out of attempts: not retried.</summary>
    Failed = 3
}

/// <summary>
/// A conversion (trial started, subscription paid) waiting to be reported to an ad platform from the server
/// (Meta Conversions API). Queued in the same transaction as the signup or payment and sent in the background.
/// </summary>
public sealed class AdConversion
{
    public const int MaxAttempts = 6;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }

    /// <summary>Platform the event goes to ("meta").</summary>
    public string Platform { get; private set; } = null!;
    public string EventName { get; private set; } = null!;

    /// <summary>Same id the website used for its copy of the event: the platform counts the conversion once.</summary>
    public string EventId { get; private set; } = null!;

    /// <summary>The event as the platform expects it (JSON).</summary>
    public string Payload { get; private set; } = null!;

    public AdConversionStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    private AdConversion() { } // EF

    public AdConversion(Guid organizationId, string platform, string eventName, string eventId, string payload, DateTime nowUtc)
    {
        OrganizationId = organizationId;
        Platform = platform;
        EventName = eventName;
        EventId = eventId;
        Payload = payload;
        Status = AdConversionStatus.Pending;
        CreatedAtUtc = nowUtc;
    }

    public void MarkSent(DateTime nowUtc)
    {
        Status = AdConversionStatus.Sent;
        Attempts++;
        SentAtUtc = nowUtc;
        NextAttemptAtUtc = null;
        LastError = null;
    }

    public void RegisterFailure(string error, bool permanent, DateTime nowUtc)
    {
        Attempts++;
        LastError = error.Length > 500 ? error[..500] : error;
        if (permanent || Attempts >= MaxAttempts)
        {
            Status = AdConversionStatus.Failed;
            NextAttemptAtUtc = null;
            return;
        }
        NextAttemptAtUtc = nowUtc.AddMinutes(Math.Pow(2, Attempts)); // 2, 4, 8, 16, 32 minutes
    }
}
