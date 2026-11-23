using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Shops;
using RepairShop.Infrastructure.Integrations;

namespace RepairShop.Api.IntegrationTests;

/// <summary>Regression tests for the findings of the security audit of 2026-11-23 (C-01, A-01, A-03, A-04).</summary>
[Collection(ApiCollection.Name)]
public sealed class SecurityAuditTests
{
    private readonly ApiFactory _factory;

    public SecurityAuditTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void C01_the_arca_ticket_cache_is_per_certificate_not_per_cuit()
    {
        var victim = new FiscalCredentials("20123456786", 3, FiscalEnvironment.Produccion, [1, 2, 3], null);
        var attacker = victim with { CertificatePfx = [9, 9, 9] };

        ArcaFiscalAuthority.CacheKey(attacker).Should().NotBe(ArcaFiscalAuthority.CacheKey(victim),
            "a shop that types someone else's CUIT must not reuse the ticket the other shop's certificate obtained");
        ArcaFiscalAuthority.CacheKey(victim with { }).Should().Be(ArcaFiscalAuthority.CacheKey(victim));
    }

    [IntegrationFact]
    public async Task A01_account_links_never_reach_the_message_tray()
    {
        var (owner, login) = await ApiClient.SignupAsync(_factory);
        var ownerEmail = login.GetProperty("user").Str("email");
        var (tech, _) = await InviteAndAcceptAsync(owner, "Tech");

        var anon = _factory.CreateClient();
        (await anon.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = ownerEmail })).IsSuccessStatusCode.Should().BeTrue();

        foreach (var viewer in new[] { tech, owner })
        {
            var tray = await (await viewer.Get("/notifications?take=200")).DataAsync();
            tray.EnumerateArray().Should().NotContain(x => x.GetProperty("relatedEntityType").ToString() == "user",
                "reset and invitation links would let a technician take over the owner's account");
            tray.EnumerateArray().Select(x => x.GetProperty("body").ToString()).Should().NotContain(b => b.Contains("token=", StringComparison.Ordinal));
        }
    }

    [IntegrationFact]
    public async Task A03_a_manual_rate_only_changes_its_own_organization()
    {
        var (a, _) = await ApiClient.SignupAsync(_factory);
        var (b, _) = await ApiClient.SignupAsync(_factory);
        const decimal poison = 1.234567m;
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-400).ToString("yyyy-MM-dd");

        await (await a.Put("/exchange-rates", new { date = day, rate = poison, baseCurrency = "USD", quoteCurrency = "ARS", source = "oficial" })).DataAsync();

        var seenByA = await (await a.Get("/exchange-rates?take=365")).DataAsync();
        seenByA.EnumerateArray().Should().Contain(r => r.GetProperty("rate").GetDecimal() == poison);
        var seenByB = await (await b.Get("/exchange-rates?take=365")).DataAsync();
        seenByB.EnumerateArray().Should().NotContain(r => r.GetProperty("rate").GetDecimal() == poison,
            "a rate typed by one shop must not become the global rate every other shop converts with");
    }

    [IntegrationFact]
    public async Task A04_a_branch_admin_cannot_reach_the_rest_of_the_organization()
    {
        var (owner, login) = await ApiClient.SignupAsync(_factory);
        var ownerId = login.GetProperty("user").Str("id");
        var branches = await (await owner.Get("/settings/branches")).DataAsync();
        var home = branches.EnumerateArray().Single().Str("id");

        var branch = await (await owner.Post("/settings/branches", new { name = "Sucursal B " + Guid.NewGuid().ToString("N")[..4], copyTemplates = false, copyCatalog = false })).DataAsync();
        var switched = await (await owner.Post("/auth/switch-shop", new { shopId = branch.Str("id") })).DataAsync();
        owner.UseToken(switched.Str("accessToken"));
        var (manager, managerId) = await InviteAndAcceptAsync(owner, "Admin"); // home branch = B

        (await manager.Post($"/users/{managerId}/shops", new { shopId = home, role = "Admin" })).StatusCode.Should().Be(HttpStatusCode.Forbidden, "B's admin cannot make itself admin of A");
        (await manager.Post($"/users/{ownerId}/reset-link")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "B's admin cannot get a link into the owner's account");
        (await manager.Put($"/users/{ownerId}", new { displayName = "Dueña Test", role = "Admin", isActive = false })).StatusCode.Should().Be(HttpStatusCode.Forbidden, "B's admin cannot deactivate the owner");
        (await manager.Post($"/settings/branches/{home}/active?value=false")).StatusCode.Should().NotBe(HttpStatusCode.NoContent, "B's admin cannot switch off the root branch");
        (await manager.Post("/billing/checkout", new { plan = "Basic" })).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the plan belongs to the root branch's admins");
        (await manager.Post("/billing/cancel")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The owner, admin of both, still manages B's people from B.
        (await owner.Put($"/users/{managerId}", new { displayName = "Encargado B", role = "Admin", isActive = true })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [IntegrationFact]
    public async Task M04_an_unconfirmed_signup_cannot_act_in_the_name_of_the_account_while_mail_works()
    {
        var (owner, _) = await ApiClient.SignupAsync(_factory, confirmEmail: false);
        (await owner.Post("/users", new { email = $"x{Guid.NewGuid():N}"[..16] + "@example.com", displayName = "Alguien", role = "Admin" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "the address was never confirmed, so it may not even be the signer's");
        (await owner.Put("/settings/integrations/mercadopago", new { enabled = false })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var order = await owner.CreateOrderAsync();
        (await owner.Post($"/orders/{order.Str("id")}/messages", new { templateKey = "order.ready", customBody = "texto libre" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(ApiClient Client, string UserId)> InviteAndAcceptAsync(ApiClient admin, string role)
    {
        var email = $"u{Guid.NewGuid():N}"[..18] + "@example.com";
        var invite = await (await admin.Post("/users", new { email, displayName = "Persona " + role, role })).DataAsync();
        var token = Uri.UnescapeDataString(invite.Str("url").Split("token=")[1]);
        var anon = _factory.CreateClient();
        await (await anon.PostAsJsonAsync("/api/v1/auth/accept-invitation", new { token, password = "ClaveNueva2026" })).DataAsync();
        return (await ApiClient.LoginAsync(_factory, email, "ClaveNueva2026"), invite.GetProperty("user").Str("id"));
    }
}
