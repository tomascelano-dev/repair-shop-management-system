using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PortalAndAdminTests
{
    private readonly ApiFactory _factory;

    public PortalAndAdminTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Portal_shows_minimal_data_and_accepts_feedback_after_delivery_only()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var order = await api.CreateOrderAsync("Batería se descarga rápido");
        var token = order.Str("trackingUrl").Split('/').Last();
        var anon = _factory.CreateClient();

        var res = await anon.GetAsync($"/api/v1/public/orders/{token}");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain("Cliente Test", "only the first name is shown").And.Contain("\"customerFirstName\":\"Cliente\"");
        res.Headers.GetValues("X-Robots-Tag").Should().Contain("noindex, nofollow");

        (await anon.PostAsJsonAsync($"/api/v1/public/orders/{token}/feedback", new { score = 5 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await (await api.Post($"/orders/{order.Str("id")}/tracking-token")).DataAsync();
        (await anon.GetAsync($"/api/v1/public/orders/{token}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "regenerating the link invalidates the old one");
    }

    [IntegrationFact]
    public async Task Admin_can_invite_users_and_the_invitation_sets_the_password()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var email = $"nuevo{Guid.NewGuid():N}"[..20] + "@local";
        var invite = await (await api.Post("/users", new { email, displayName = "Nuevo Técnico", role = "Tech" })).DataAsync();
        var token = Uri.UnescapeDataString(invite.Str("url").Split("token=")[1]);

        var anon = _factory.CreateClient();
        var info = await (await anon.GetAsync($"/api/v1/auth/token-info?token={Uri.EscapeDataString(token)}")).DataAsync();
        info.Str("email").Should().Be(email);

        var accepted = await (await anon.PostAsJsonAsync("/api/v1/auth/accept-invitation", new { token, password = "ClaveNueva2026" })).DataAsync();
        accepted.GetProperty("user").Str("role").Should().Be("Tech");

        (await anon.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "ClaveNueva2026" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [IntegrationFact]
    public async Task Pdf_documents_are_generated()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var order = await api.CreateOrderAsync();
        foreach (var doc in new[] { "intake", "label" })
        {
            var res = await api.Get($"/orders/{order.Str("id")}/documents/{doc}");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            res.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            (await res.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal("%PDF"u8.ToArray());
        }
    }

    [IntegrationFact]
    public async Task Reports_and_dashboard_respond()
    {
        var api = await ApiClient.LoginAsync(_factory);
        foreach (var path in new[] { "/dashboard/summary", "/dashboard/revenue?days=30", "/reports/revenue", "/reports/margins", "/reports/repair-times",
                     "/reports/quotes", "/reports/top-issues", "/reports/technicians", "/reports/warranty", "/reports/feedback", "/reports/inventory" })
        {
            (await api.Get(path)).StatusCode.Should().Be(HttpStatusCode.OK, path);
        }

        var xlsx = await api.Get("/reports/margins/export");
        (await xlsx.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal("PK"u8.ToArray());
    }
}
