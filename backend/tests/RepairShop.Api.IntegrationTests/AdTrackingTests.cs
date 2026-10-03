using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Billing;
using RepairShop.Domain.Billing;
using RepairShop.Infrastructure.Billing;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AdTrackingTests
{
    private readonly ApiFactory _factory;
    public AdTrackingTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Public_config_exposes_tag_ids_but_never_the_server_token()
    {
        var http = _factory.CreateClient();
        var response = await http.GetAsync("/api/v1/billing/config");
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("123456789").And.NotContain("integration-private-meta-token").And.NotContain("metaCapiToken");
        (await http.PostAsync("/api/v1/billing/ad-consent/revoke", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [IntegrationFact]
    public async Task Consented_conversions_are_hashed_deduplicated_retried_and_stopped_after_withdrawal()
    {
        var eventId = "trial_" + Guid.NewGuid().ToString("N");
        var email = "ads" + Guid.NewGuid().ToString("N") + "@example.com";
        var (owner, login) = await ApiClient.SignupAsync(_factory, "US", email, new
        {
            utmSource = "google", utmCampaign = "launch", gclid = "test-click", adConsent = true, eventId,
        });
        var org = Guid.Parse(login.GetProperty("user").Str("organizationId"));
        var (_, otherLogin) = await ApiClient.SignupAsync(_factory, "US", attribution: new { adConsent = false, eventId = "trial_denied" });
        var otherOrg = Guid.Parse(otherLogin.GetProperty("user").Str("organizationId"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
        var attribution = await db.SignupAttributions.AsNoTracking().SingleAsync(x => x.OrganizationId == org);
        attribution.Source.Should().Be("google");
        attribution.Campaign.Should().Be("launch");
        (await db.AdConversions.AnyAsync(x => x.OrganizationId == otherOrg)).Should().BeFalse();
        var trial = await db.AdConversions.SingleAsync(x => x.OrganizationId == org);
        trial.EventId.Should().Be(eventId);
        using var payload = JsonDocument.Parse(trial.Payload);
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email))).ToLowerInvariant();
        payload.RootElement.GetProperty("user_data").GetProperty("em")[0].GetString().Should().Be(expectedHash);
        trial.Payload.Should().NotContain(email);

        var fake = new MetaHttp();
        var dispatcher = new MetaConversionsDispatcher(db, fake,
            scope.ServiceProvider.GetRequiredService<IDateTimeProvider>(),
            scope.ServiceProvider.GetRequiredService<IOptions<TrackingOptions>>(), NullLogger<MetaConversionsDispatcher>.Instance);
        await dispatcher.RunOnceAsync(default);
        trial.Status.Should().Be(AdConversionStatus.Sent);
        fake.Bodies.Should().ContainSingle();
        fake.Bodies[0].Should().Contain(eventId);

        var subscriptionId = "sub_" + Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var http = _factory.CreateClient();
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Paddle(http, JsonSerializer.Serialize(new
        {
            event_id = $"evt_{i}", event_type = "subscription.updated", occurred_at = now.AddMilliseconds(i).ToString("O"),
            data = new
            {
                id = subscriptionId, status = "active", currency_code = "USD",
                custom_data = new { organization_id = org, plan = "Basic" },
                current_billing_period = new { ends_at = now.AddMonths(1).ToString("O") },
                items = new[] { new { price = new { id = "pri_basic", unit_price = new { amount = "2500", currency_code = "USD" } } } },
            },
        }))));
        replies.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        var purchases = await db.AdConversions.Where(x => x.OrganizationId == org && x.EventName == "Purchase").ToListAsync();
        purchases.Should().ContainSingle();
        purchases[0].EventId.Should().Be("purchase_" + subscriptionId);
        var subscription = await (await owner.Get("/billing/subscription")).DataAsync();
        subscription.Str("conversionId").Should().Be(purchases[0].EventId);

        fake.Status = HttpStatusCode.TooManyRequests;
        await dispatcher.RunOnceAsync(default);
        purchases[0].Status.Should().Be(AdConversionStatus.Pending);
        purchases[0].NextAttemptAtUtc.Should().BeAfter(now);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ad_conversions SET \"NextAttemptAtUtc\" = NULL WHERE \"OrganizationId\" = {org} AND \"Status\" = 1");
        db.ChangeTracker.Clear();
        (await owner.Post("/billing/ad-consent/revoke")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var sends = fake.Bodies.Count;
        await dispatcher.RunOnceAsync(default);
        fake.Bodies.Count.Should().Be(sends, "withdrawn consent prevents delivery of queued events");
        (await db.AdConversions.AsNoTracking().SingleAsync(x => x.OrganizationId == org && x.EventName == "Purchase")).Status.Should().Be(AdConversionStatus.Failed);
        (await db.SignupAttributions.AsNoTracking().SingleAsync(x => x.OrganizationId == org)).AdConsent.Should().BeFalse();
        (await db.SignupAttributions.AsNoTracking().SingleAsync(x => x.OrganizationId == otherOrg)).AdConsent.Should().BeFalse();
    }

    private static Task<HttpResponseMessage> Paddle(HttpClient http, string body)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhooks/paddle") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Paddle-Signature", $"ts={ts};h1={PaddleSignature.Sign(ApiFactory.PaddleWebhookSecret, ts, body)}");
        return http.SendAsync(request);
    }

    private sealed class MetaHttp : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Bodies { get; } = new();
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Status) { Content = new StringContent(Status == HttpStatusCode.OK ? "{\"events_received\":1}" : "{\"error\":{\"message\":\"rate limited\"}}") };
        }
    }
}
