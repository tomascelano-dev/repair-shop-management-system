using FluentAssertions;
using RepairShop.Application.Billing;
using RepairShop.Domain.Billing;

namespace RepairShop.Application.Tests;

public sealed class BillingOptionsTests
{
    private static MercadoPagoAccountOptions Account(bool credentials = true, bool allPrices = true)
    {
        var a = new MercadoPagoAccountOptions { Prices = new() { ["Basic"] = 100, ["Standard"] = 200 } };
        if (allPrices) a.Prices["Pro"] = 300;
        if (credentials) { a.AccessToken = "APP_USR-test"; a.WebhookSecret = "secret"; }
        return a;
    }

    [Fact]
    public void Only_mercado_pago_countries_with_credentials_and_every_price_are_offered()
    {
        var o = new BillingOptions
        {
            Countries = new()
            {
                ["MX"] = Account(),
                ["AR"] = Account(),
                ["CL"] = Account(credentials: false),
                ["PE"] = Account(allPrices: false),
                ["US"] = Account(), // Mercado Pago does not sell subscriptions there
            }
        };

        o.OfferedCountries().Should().Equal("AR", "MX");
        o.IsOffered("ar").Should().BeTrue();
        o.IsOffered("CL").Should().BeFalse();
        o.IsOffered("US").Should().BeFalse();
        o.IsOffered(null).Should().BeFalse();
        o.PriceFor(PlanId.Standard, "MX").Should().Be(200);
        o.PriceFor(PlanId.Pro, "PE").Should().BeNull();
    }

    [Fact]
    public void Simulated_charges_offer_countries_without_credentials_outside_production()
    {
        var o = new BillingOptions { AllowSimulated = true, Countries = new() { ["UY"] = Account(credentials: false) } };
        o.OfferedCountries().Should().Equal("UY");
    }

    [Fact]
    public void Configuration_keys_are_matched_ignoring_case()
    {
        // The configuration binder can rebuild the dictionaries with the default, case-sensitive comparer.
        var o = new BillingOptions { Countries = new(StringComparer.Ordinal) { ["ar"] = Account() } };
        o.Countries["ar"].Prices = new(StringComparer.Ordinal) { ["basic"] = 1, ["STANDARD"] = 2, ["Pro"] = 3 };

        o.IsOffered("AR").Should().BeTrue();
        o.PriceFor(PlanId.Basic, "AR").Should().Be(1);
    }

    [Theory]
    [InlineData("AR", "ARS")]
    [InlineData("br", "BRL")]
    [InlineData("MX", "MXN")]
    [InlineData("US", null)]
    public void Each_mercado_pago_country_charges_in_its_currency(string country, string? currency)
        => MercadoPagoCountries.CurrencyFor(country).Should().Be(currency);
}
