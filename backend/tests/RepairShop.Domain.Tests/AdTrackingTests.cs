using FluentAssertions;
using RepairShop.Domain.Billing;

namespace RepairShop.Domain.Tests;

public sealed class AdTrackingTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Attribution_limits_untrusted_campaign_data_and_allows_consent_withdrawal()
    {
        var a = new SignupAttribution(Guid.NewGuid(), Guid.NewGuid(),
            new SignupAttributionData(Source: "  google\n ", Campaign: new string('x', 500), EventId: "trial_1", AdConsent: true), null, null, Now);
        a.Source.Should().Be("google");
        a.Campaign.Should().HaveLength(200);
        a.FromCampaign.Should().BeTrue();
        a.AdConsent.Should().BeTrue();
        a.RevokeAdConsent();
        a.AdConsent.Should().BeFalse();
    }

    [Fact]
    public void Temporary_failures_back_off_and_stop_after_six_attempts()
    {
        var e = new AdConversion(Guid.NewGuid(), "meta", "StartTrial", "trial_1", "{}", Now);
        e.RegisterFailure("timeout", false, Now);
        e.NextAttemptAtUtc.Should().Be(Now.AddMinutes(2));
        e.Status.Should().Be(AdConversionStatus.Pending);
        for (var i = 1; i < AdConversion.MaxAttempts; i++) e.RegisterFailure("timeout", false, Now);
        e.Status.Should().Be(AdConversionStatus.Failed);
        e.NextAttemptAtUtc.Should().BeNull();
    }

    [Fact]
    public void Permanent_failure_is_not_retried_and_error_messages_are_bounded()
    {
        var e = new AdConversion(Guid.NewGuid(), "meta", "Purchase", "purchase_sub", "{}", Now);
        e.RegisterFailure(new string('x', 1000), true, Now);
        e.Status.Should().Be(AdConversionStatus.Failed);
        e.LastError.Should().HaveLength(500);
        e.NextAttemptAtUtc.Should().BeNull();
    }
}
