using FluentAssertions;
using RepairShop.Domain.Common;

namespace RepairShop.Domain.Tests;

public class ValueTests
{
    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(2.345, 2.35)]
    [InlineData(-1.005, -1.01)]
    [InlineData(10, 10)]
    public void Money_rounds_half_away_from_zero(decimal value, decimal expected) => Money.Round(value).Should().Be(expected);

    [Theory]
    [InlineData("ars", "ARS")]
    [InlineData(" usd ", "USD")]
    public void Currency_is_normalized(string input, string expected) => Money.NormalizeCurrency(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("PESOS")]
    [InlineData("U$S")]
    public void Invalid_currency_is_rejected(string input)
        => ((Action)(() => Money.NormalizeCurrency(input))).Should().Throw<DomainException>();

    [Theory]
    [InlineData("011 15 2345-6789", "5491123456789")]
    [InlineData("11 2345 6789", "5491123456789")]
    [InlineData("+54 9 11 2345 6789", "5491123456789")]
    [InlineData("0351 15 555 1212", "5493515551212")]
    [InlineData("+1 415 555 2671", "14155552671")]
    public void Whatsapp_digits_follow_argentine_rules(string phone, string expected)
        => PhoneNumber.ToWhatsAppDigits(phone).Should().Be(expected);
}
