using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using RepairShop.Api.Saas;
using RepairShop.Domain.Saas;

namespace RepairShop.Api.Tests;

public sealed class SaasRulesTests
{
    [Theory]
    [InlineData("Monotributo", "ConsumidorFinal", false, 11)]
    [InlineData("Monotributo", "ResponsableInscripto", true, 13)]
    [InlineData("ResponsableInscripto", "ResponsableInscripto", false, 1)]
    [InlineData("ResponsableInscripto", "Monotributo", false, 1)]
    [InlineData("ResponsableInscripto", "ConsumidorFinal", false, 6)]
    [InlineData("ResponsableInscripto", "Exento", true, 8)]
    public void Voucher_type_follows_emitter_and_receiver_vat_condition(string emitter, string receiver, bool creditNote, int expected) =>
        FiscalRules.VoucherType(emitter, receiver, creditNote).Should().Be(expected);

    [Fact]
    public void Invoice_b_splits_vat_included_prices()
    {
        var (net, vat) = FiscalRules.Split(6, 121m, 21m);
        net.Should().Be(100m); vat.Should().Be(21m);
        FiscalRules.Split(11, 121m, 21m).Should().Be((121m, 0m));
    }

    [Theory]
    [InlineData("20123456786", true)]
    [InlineData("20123456787", false)]
    [InlineData("30500010912", true)]
    [InlineData("123", false)]
    public void Cuit_check_digit(string cuit, bool valid) => FiscalRules.ValidCuit(cuit).Should().Be(valid);

    private static ShopSubscription Sub(string status, DateTime trialEnd, DateTime? periodEnd = null) =>
        new() { ShopId = Guid.NewGuid(), Plan = "Basic", Status = status, TrialEndsAtUtc = trialEnd, CurrentPeriodEndsAtUtc = periodEnd };

    [Fact]
    public void Expired_trial_and_unpaid_subscriptions_become_read_only()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new SaasOptions { GraceDays = 7 };
        SubscriptionService.Evaluate(Sub("Trialing", now.AddDays(1)), options, now).ReadOnly.Should().BeFalse();
        SubscriptionService.Evaluate(Sub("Trialing", now.AddDays(-1)), options, now).ReadOnly.Should().BeTrue();
        SubscriptionService.Evaluate(Sub("PastDue", now, now.AddDays(-3)), options, now).ReadOnly.Should().BeFalse();
        SubscriptionService.Evaluate(Sub("PastDue", now, now.AddDays(-8)), options, now).ReadOnly.Should().BeTrue();
        SubscriptionService.Evaluate(Sub("Cancelled", now, now.AddDays(5)), options, now).ReadOnly.Should().BeFalse();
        SubscriptionService.Evaluate(Sub("Active", now.AddDays(-30), now.AddDays(20)), options, now).Modules.Should().NotContain("stock");
    }

    [Theory]
    [InlineData("/api/v2/premium/stock/receive", "purchases")]
    [InlineData("/api/v2/premium/stock/reserve", "stock")]
    [InlineData("/api/v2/premium/workspace", null)]
    [InlineData("/api/saas/sales", "cash")]
    [InlineData("/api/saas/integrations/keys", "api")]
    [InlineData("/api/saas/me", null)]
    [InlineData("/api/v2/orders", null)]
    public void Paths_map_to_plan_modules(string path, string? module) => Plans.ModuleForPath(path).Should().Be(module);

    [Fact]
    public void Every_plan_includes_the_previous_one()
    {
        var o = new SaasOptions();
        Plans.Get("Standard", o).Modules.Should().Contain(Plans.Get("Basic", o).Modules);
        Plans.Get("Pro", o).Modules.Should().Contain(Plans.Get("Standard", o).Modules);
    }

    [Fact]
    public void Slots_skip_closed_days_busy_times_and_the_past()
    {
        var hours = new OpeningHours([1], "09:00", "11:00", 30); // Mondays only
        var monday = new DateOnly(2026, 10, 5);
        var now = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var busy = new[] { (new DateTime(2026, 10, 5, 12, 30, 0, DateTimeKind.Utc), 30) }; // 09:30 local
        var slots = AgendaRules.Slots(hours, monday, busy, now);
        slots.Select(s => s.ToString("HH:mm")).Should().Equal("12:00", "13:00", "13:30");
        AgendaRules.Slots(hours, monday.AddDays(1), [], now).Should().BeEmpty();
    }

    [Fact]
    public void Mercado_pago_signature_is_verified()
    {
        const string secret = "s3cret";
        var manifest = "id:123abc;request-id:req-1;ts:1700000000;";
        var v1 = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        MercadoPagoBillingProvider.VerifySignature(secret, $"ts=1700000000,v1={v1}", "req-1", "123ABC").Should().BeTrue();
        MercadoPagoBillingProvider.VerifySignature(secret, $"ts=1700000001,v1={v1}", "req-1", "123abc").Should().BeFalse();
        MercadoPagoBillingProvider.VerifySignature("", $"ts=1700000000,v1={v1}", "req-1", "123abc").Should().BeFalse();
    }

    [Theory]
    [InlineData("11 5555-0000", "+5491155550000")]
    [InlineData("011 4444-5555", "+5491144445555")]
    [InlineData("+34 600 000 000", "+34600000000")]
    [InlineData("", "")]
    public void Phones_are_normalized_to_e164(string input, string expected) => Notifier.NormalizePhone(input).Should().Be(expected);

    [Fact]
    public void Slugs_are_url_safe()
    {
        ShopProvisioning.Slugify("Técnico Ñandú & Cía.").Should().Be("tecnico-nandu-cia");
        ShopProvisioning.Slugify("!!").Should().StartWith("taller-");
    }
}
