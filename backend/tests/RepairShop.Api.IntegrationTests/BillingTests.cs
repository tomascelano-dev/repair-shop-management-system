using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Npgsql;
using RepairShop.Application.Billing;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class BillingTests
{
    private readonly ApiFactory _factory;

    public BillingTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Signup_creates_the_shop_with_a_free_trial_and_signs_the_owner_in()
    {
        var (owner, login) = await ApiClient.SignupAsync(_factory, "MX");

        login.GetProperty("user").GetProperty("role").GetString().Should().Be("Admin");
        login.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeFalse();

        var sub = await (await owner.Get("/billing/subscription")).DataAsync();
        sub.Str("status").Should().Be("Trialing");
        sub.Str("plan").Should().Be("Pro");
        sub.GetProperty("trialDaysLeft").GetInt32().Should().Be(14);
        sub.Str("billingCountry").Should().Be("MX");
        sub.GetProperty("plans")[0].Str("currency").Should().Be("MXN");
        sub.GetProperty("checkoutAvailable").GetBoolean().Should().BeTrue();

        var settings = await (await owner.Get("/settings")).DataAsync();
        settings.ToString().Should().Contain("MXN").And.Contain("America/Mexico_City");

        // The owner works right away (templates were provisioned too).
        await owner.CreateOrderAsync("Pantalla rota");
        var templates = await (await owner.Get("/templates")).DataAsync();
        templates.GetArrayLength().Should().BeGreaterThan(0);
    }

    [IntegrationFact]
    public async Task Signup_rejects_an_email_that_already_has_an_account_and_requires_the_terms()
    {
        var email = $"dup{Guid.NewGuid():N}"[..20] + "@example.com";
        await ApiClient.SignupAsync(_factory, "AR", email);
        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            shopName = "Otro taller", ownerName = "Otra persona", email = email.ToUpperInvariant(), password = "Clave12345", country = "AR", acceptTerms = true
        });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);

        res = await anon.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            shopName = "Otro taller", ownerName = "Otra persona", email = "nuevo-sin-terminos@example.com", password = "Clave12345", country = "AR", acceptTerms = false
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [IntegrationTheory]
    [InlineData("US")] // Mercado Pago does not sell subscriptions there
    [InlineData("ES")]
    [InlineData("CL")] // Mercado Pago country without a configured account
    public async Task Signup_is_only_open_to_countries_where_mercado_pago_charges(string country)
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            shopName = "Taller de afuera", ownerName = "Persona", email = $"out{Guid.NewGuid():N}"[..20] + "@example.com", password = "Clave12345", country, acceptTerms = true
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Regex.Unescape(await res.Content.ReadAsStringAsync()).Should().Contain("Argentina y México", "the message lists the countries where RepairShop is sold");
    }

    [IntegrationFact]
    public async Task The_emailed_link_verifies_the_address()
    {
        var email = $"verify{Guid.NewGuid():N}"[..20] + "@example.com";
        var (owner, _) = await ApiClient.SignupAsync(_factory, "AR", email);

        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT \"Body\" FROM notification_outbox WHERE \"Recipient\" = @r ORDER BY \"CreatedAtUtc\" DESC LIMIT 1", conn);
        cmd.Parameters.AddWithValue("r", email);
        var body = (string)(await cmd.ExecuteScalarAsync())!;
        var token = Uri.UnescapeDataString(Regex.Match(body, @"verificar-email\?token=([^\s]+)").Groups[1].Value);

        var anon = _factory.CreateClient();
        (await anon.PostAsJsonAsync("/api/v1/auth/verify-email", new { token = "otro" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await anon.PostAsJsonAsync("/api/v1/auth/verify-email", new { token })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await (await owner.Get("/auth/me")).DataAsync();
        me.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeTrue();
    }

    [IntegrationFact]
    public async Task When_the_trial_ends_the_account_is_read_only_until_a_plan_is_paid()
    {
        var (owner, login) = await ApiClient.SignupAsync(_factory, "AR");
        await EndTrialAsync(login);

        (await owner.Get("/customers")).StatusCode.Should().Be(HttpStatusCode.OK, "reading stays available");
        var res = await owner.Post("/customers", new { fullName = "Cliente nuevo", phone = "11 5555-1234" });
        res.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await res.Content.ReadAsStringAsync()).Should().Contain("subscription_inactive");

        // Argentina pays with Mercado Pago; it is not configured in tests, so the simulated provider activates the plan.
        var checkout = await (await owner.Post("/billing/checkout", new { plan = "Standard" })).DataAsync();
        checkout.GetProperty("changed").GetBoolean().Should().BeTrue();

        var sub = await (await owner.Get("/billing/subscription")).DataAsync();
        sub.Str("status").Should().Be("Active");
        sub.Str("plan").Should().Be("Standard");
        sub.Str("currency").Should().Be("ARS");
        (await owner.Post("/customers", new { fullName = "Cliente nuevo", phone = "11 5555-1234" })).IsSuccessStatusCode.Should().BeTrue();
    }

    [IntegrationFact]
    public async Task Basic_plan_blocks_modules_and_extra_branches()
    {
        var (owner, _) = await ApiClient.SignupAsync(_factory, "AR");
        await (await owner.Post("/billing/checkout", new { plan = "Basic" })).DataAsync();

        var res = await owner.Get("/reports/revenue");
        res.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        problem.Str("code").Should().Be("plan_upgrade_required");
        problem.Str("module").Should().Be("reports");

        (await owner.Get("/suppliers")).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await owner.Post("/settings/branches", new { name = "Sucursal 2", copyTemplates = true })).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await owner.Get("/orders")).StatusCode.Should().Be(HttpStatusCode.OK, "core features are in every plan");
    }

    [IntegrationFact]
    public async Task Mercado_pago_checkout_activates_the_plan_only_after_a_signed_notification()
    {
        var (owner, _) = await ApiClient.SignupAsync(_factory, "MX");

        var checkout = await (await owner.Post("/billing/checkout", new { plan = "Standard" })).DataAsync();
        checkout.GetProperty("changed").GetBoolean().Should().BeFalse();
        var url = checkout.Str("url");
        url.Should().StartWith("https://www.mercadopago.com.mx/subscriptions/checkout?preapproval_id=");
        var preapprovalId = url.Split('=')[1];
        var created = _factory.MercadoPago.Preapproval(preapprovalId)!;
        created["auto_recurring"]!["transaction_amount"]!.GetValue<decimal>().Should().Be(799m);
        created["auto_recurring"]!["currency_id"]!.GetValue<string>().Should().Be("MXN");

        // Not paid yet: still on the trial.
        (await (await owner.Get("/billing/subscription")).DataAsync()).Str("status").Should().Be("Trialing");

        _factory.MercadoPago.Authorize(preapprovalId, DateTime.UtcNow.AddMonths(1));
        var anon = _factory.CreateClient();
        var forged = FakeMercadoPago.Notification("/api/v1/billing/webhooks/mercadopago/mx", "subscription_preapproval", preapprovalId, "wrong-secret");
        (await anon.SendAsync(forged)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var toArgentina = FakeMercadoPago.Notification("/api/v1/billing/webhooks/mercadopago", "subscription_preapproval", preapprovalId, ApiFactory.MexicoWebhookSecret);
        (await anon.SendAsync(toArgentina)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "Argentina's account is not configured in tests");
        var genuine = FakeMercadoPago.Notification("/api/v1/billing/webhooks/mercadopago/mx", "subscription_preapproval", preapprovalId, ApiFactory.MexicoWebhookSecret);
        (await anon.SendAsync(genuine)).StatusCode.Should().Be(HttpStatusCode.OK);

        var sub = await (await owner.Get("/billing/subscription")).DataAsync();
        sub.Str("status").Should().Be("Active");
        sub.Str("plan").Should().Be("Standard");
        sub.Str("provider").Should().Be("MercadoPago");
        sub.Dec("amount").Should().Be(799m);
        sub.Str("currency").Should().Be("MXN");
        sub.GetProperty("canManageAtProvider").GetBoolean().Should().BeTrue();

        // Plan change updates the amount at Mercado Pago; cancelling stops the charges there.
        (await (await owner.Post("/billing/checkout", new { plan = "Pro" })).DataAsync()).GetProperty("changed").GetBoolean().Should().BeTrue();
        _factory.MercadoPago.Preapproval(preapprovalId)!["auto_recurring"]!["transaction_amount"]!.GetValue<decimal>().Should().Be(1399m);
        (await (await owner.Post("/billing/cancel")).DataAsync()).Str("status").Should().Be("Canceled");
        _factory.MercadoPago.Preapproval(preapprovalId)!["status"]!.GetValue<string>().Should().Be("cancelled");
    }

    [IntegrationFact]
    public async Task Notifications_for_the_same_subscription_arriving_together_are_all_accepted()
    {
        var (owner, _) = await ApiClient.SignupAsync(_factory, "MX");
        var url = (await (await owner.Post("/billing/checkout", new { plan = "Basic" })).DataAsync()).Str("url");
        var preapprovalId = url.Split('=')[1];
        _factory.MercadoPago.Authorize(preapprovalId, DateTime.UtcNow.AddMonths(1));
        var payment = _factory.MercadoPago.AddAuthorizedPayment(preapprovalId);

        var anon = _factory.CreateClient();
        var path = "/api/v1/billing/webhooks/mercadopago/MX";
        var responses = await Task.WhenAll(
            anon.SendAsync(FakeMercadoPago.Notification(path, "subscription_preapproval", preapprovalId, ApiFactory.MexicoWebhookSecret)),
            anon.SendAsync(FakeMercadoPago.Notification(path, "subscription_preapproval", preapprovalId, ApiFactory.MexicoWebhookSecret)),
            anon.SendAsync(FakeMercadoPago.Notification(path, "subscription_authorized_payment", payment, ApiFactory.MexicoWebhookSecret)),
            anon.SendAsync(FakeMercadoPago.Notification(path, "payment", "999", ApiFactory.MexicoWebhookSecret)));

        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        var sub = await (await owner.Get("/billing/subscription")).DataAsync();
        sub.Str("status").Should().Be("Active");
        sub.Str("plan").Should().Be("Basic");
    }

    [IntegrationFact]
    public async Task Public_price_list_uses_the_country_currency_and_falls_back_to_argentina()
    {
        var anon = _factory.CreateClient();
        var ar = await (await anon.GetAsync("/api/v1/billing/plans?country=AR")).DataAsync();
        ar.Str("currency").Should().Be("ARS");
        ar.GetProperty("plans").GetArrayLength().Should().Be(3);

        var mx = await (await anon.GetAsync("/api/v1/billing/plans?country=mx")).DataAsync();
        mx.Str("country").Should().Be("MX");
        mx.Str("currency").Should().Be("MXN");
        mx.GetProperty("plans")[0].Dec("price").Should().Be(449m);

        foreach (var other in new[] { "", "?country=US", "?country=xyz" })
        {
            var fallback = await (await anon.GetAsync("/api/v1/billing/plans" + other)).DataAsync();
            fallback.Str("country").Should().Be("AR");
            fallback.Str("currency").Should().Be("ARS");
        }

        var config = await (await anon.GetAsync("/api/v1/billing/config")).DataAsync();
        config.GetProperty("signupEnabled").GetBoolean().Should().BeTrue();
        config.GetProperty("countries").EnumerateArray().Select(c => c.Str("code") + ":" + c.Str("currency"))
            .Should().Equal("AR:ARS", "MX:MXN");
        config.ToString().Should().NotContain("paddle", "Paddle was removed").And.NotContain(ApiFactory.MexicoAccessToken);
        // The Paddle webhook is gone: it answers like any route that does not exist.
        var unknown = (await anon.PostAsync("/api/v1/billing/webhooks/no-existe", new StringContent("{}"))).StatusCode;
        (await anon.PostAsync("/api/v1/billing/webhooks/paddle", new StringContent("{}"))).StatusCode.Should().Be(unknown).And.NotBe(HttpStatusCode.OK);
    }

    private async Task EndTrialAsync(JsonElement login)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("UPDATE subscriptions SET \"TrialEndsAtUtc\" = now() - interval '1 day' WHERE \"OrganizationId\" = @o", conn);
        cmd.Parameters.AddWithValue("o", Guid.Parse(login.GetProperty("user").Str("organizationId")));
        (await cmd.ExecuteNonQueryAsync()).Should().Be(1);
    }
}
