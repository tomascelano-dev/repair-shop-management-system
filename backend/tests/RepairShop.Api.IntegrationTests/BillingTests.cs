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
        sub.GetProperty("plans")[0].Str("currency").Should().Be("USD");

        var settings = await (await owner.Get("/settings")).DataAsync();
        settings.ToString().Should().Contain("USD").And.Contain("America/Mexico_City");

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
    public async Task Paddle_webhooks_activate_the_plan_only_with_a_valid_signature()
    {
        var (owner, login) = await ApiClient.SignupAsync(_factory, "ES");
        var org = login.GetProperty("user").Str("organizationId");
        var anon = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            event_id = "evt_1",
            event_type = "subscription.created",
            occurred_at = DateTime.UtcNow.ToString("O"),
            data = new
            {
                id = "sub_01test",
                status = "active",
                customer_id = "ctm_01test",
                currency_code = "USD",
                custom_data = new { organization_id = org, plan = "Basic" },
                current_billing_period = new { starts_at = DateTime.UtcNow.ToString("O"), ends_at = DateTime.UtcNow.AddMonths(1).ToString("O") },
                items = new[] { new { price = new { id = "pri_standard", unit_price = new { amount = "4500", currency_code = "USD" } } } }
            }
        });

        (await PostPaddleAsync(anon, body, "wrong-secret")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PostPaddleAsync(anon, body, ApiFactory.PaddleWebhookSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        var sub = await (await owner.Get("/billing/subscription")).DataAsync();
        sub.Str("status").Should().Be("Active");
        sub.Str("plan").Should().Be("Standard", "the price id decides the plan");
        sub.Str("provider").Should().Be("Paddle");
        sub.Dec("amount").Should().Be(45m);
        sub.GetProperty("canManageAtProvider").GetBoolean().Should().BeTrue();
    }

    [IntegrationFact]
    public async Task Public_price_list_is_in_pesos_for_argentina_and_dollars_elsewhere()
    {
        var anon = _factory.CreateClient();
        var ar = await (await anon.GetAsync("/api/v1/billing/plans?country=AR")).DataAsync();
        ar.Str("currency").Should().Be("ARS");
        ar.GetProperty("plans").GetArrayLength().Should().Be(3);

        var us = await (await anon.GetAsync("/api/v1/billing/plans")).DataAsync();
        us.Str("currency").Should().Be("USD");
        us.GetProperty("plans")[0].Dec("price").Should().BeGreaterThan(0);

        var config = await (await anon.GetAsync("/api/v1/billing/config")).DataAsync();
        config.GetProperty("signupEnabled").GetBoolean().Should().BeTrue();
        config.Str("paddleClientToken").Should().Be("test_client_token");
    }

    private async Task EndTrialAsync(JsonElement login)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("UPDATE subscriptions SET \"TrialEndsAtUtc\" = now() - interval '1 day' WHERE \"OrganizationId\" = @o", conn);
        cmd.Parameters.AddWithValue("o", Guid.Parse(login.GetProperty("user").Str("organizationId")));
        (await cmd.ExecuteNonQueryAsync()).Should().Be(1);
    }

    private static Task<HttpResponseMessage> PostPaddleAsync(HttpClient http, string body, string secret)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhooks/paddle") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.Add("Paddle-Signature", $"ts={ts};h1={PaddleSignature.Sign(secret, ts, body)}");
        return http.SendAsync(req);
    }
}
