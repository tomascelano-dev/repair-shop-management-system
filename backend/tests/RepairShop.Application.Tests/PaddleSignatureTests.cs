using FluentAssertions;
using RepairShop.Application.Billing;

namespace RepairShop.Application.Tests;

public sealed class PaddleSignatureTests
{
    private const string Secret = "pdl_ntfset_test_secret";
    private const string Body = "{\"event_type\":\"subscription.created\"}";
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    private static string Header(DateTime when, string body = Body, string secret = Secret)
    {
        var ts = new DateTimeOffset(when).ToUnixTimeSeconds().ToString();
        return $"ts={ts};h1={PaddleSignature.Sign(secret, ts, body)}";
    }

    [Fact]
    public void Valid_signature_is_accepted() => PaddleSignature.IsValid(Secret, Header(Now), Body, Now).Should().BeTrue();

    [Fact]
    public void Tampered_body_is_rejected() => PaddleSignature.IsValid(Secret, Header(Now), Body + " ", Now).Should().BeFalse();

    [Fact]
    public void Wrong_secret_is_rejected() => PaddleSignature.IsValid(Secret, Header(Now, secret: "other"), Body, Now).Should().BeFalse();

    [Fact]
    public void Old_signature_is_rejected() => PaddleSignature.IsValid(Secret, Header(Now.AddMinutes(-10)), Body, Now).Should().BeFalse();

    [Fact]
    public void Any_of_several_signatures_can_match_while_the_secret_rotates()
    {
        var header = Header(Now);
        PaddleSignature.IsValid(Secret, header.Replace(";h1=", ";h1=deadbeef;h1="), Body, Now).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("h1=abc")]
    [InlineData("ts=notanumber;h1=abc")]
    public void Malformed_headers_are_rejected(string? header) => PaddleSignature.IsValid(Secret, header, Body, Now).Should().BeFalse();
}
