using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Login_returns_permissions_and_sets_httponly_refresh_cookie()
    {
        var http = _factory.CreateClient();
        var res = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@local", password = "Admin123456" });
        var data = await res.DataAsync();

        data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Contain("admin");
        var cookie = res.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rs_refresh="));
        cookie.Should().Contain("httponly").And.Contain("path=/api/v1/auth").And.Contain("samesite=strict");
    }

    [IntegrationFact]
    public async Task Wrong_password_is_401_in_spanish()
    {
        var http = _factory.CreateClient();
        var res = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "tech@local", password = "incorrecta123" });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).Should().Contain("incorrectos");
    }

    [IntegrationFact]
    public async Task Refresh_requires_csrf_header_and_rotates_the_token()
    {
        var client = await ApiClient.LoginAsync(_factory);

        var withoutHeader = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        client.Http.DefaultRequestHeaders.Remove("X-RS-Refresh");
        (await client.Http.SendAsync(withoutHeader)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.Http.DefaultRequestHeaders.Add("X-RS-Refresh", "1");

        var first = await (await client.Post("/auth/refresh")).DataAsync();
        var second = await (await client.Post("/auth/refresh")).DataAsync();
        first.Str("accessToken").Should().NotBe(second.Str("accessToken"));
    }

    [IntegrationFact]
    public async Task Logout_all_invalidates_existing_access_tokens()
    {
        var client = await ApiClient.LoginAsync(_factory, "recepcion@local", "Recepcion123");
        (await client.Get("/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.Post("/auth/logout-all")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.Get("/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.Post("/auth/refresh")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [IntegrationFact]
    public async Task Role_permissions_are_enforced()
    {
        var cashier = await ApiClient.LoginAsync(_factory, "caja@local", "Caja123456");
        (await cashier.Get("/sales/catalog")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cashier.Get("/reports/revenue")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cashier.Get("/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cashier.Post("/inventory", new { sku = "NOPE-1", name = "No" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var tech = await ApiClient.LoginAsync(_factory, "tech@local", "Tech123456");
        (await tech.Get("/sales/catalog")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tech.Get("/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [IntegrationFact]
    public async Task Anonymous_requests_are_rejected_except_public_endpoints()
    {
        var http = _factory.CreateClient();
        (await http.GetAsync("/api/v1/orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await http.GetAsync("/api/v1/public/orders/no-existe")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await http.GetAsync("/healthz")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
