using FluentAssertions;
using RepairShop.Application.Cash;
using RepairShop.Application.Contracts;
using RepairShop.Application.Files;
using RepairShop.Application.Fiscal;
using RepairShop.Application.Imports;
using RepairShop.Application.Payments;
using RepairShop.Application.RepairOrders;
using RepairShop.Application.Security;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Users;
using RepairShop.Application.Abstractions;

namespace RepairShop.Application.Tests;

public class MercadoPagoSignatureTests
{
    private const string Secret = "secreto-del-webhook";
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Valid_signature_is_accepted()
    {
        var ts = new DateTimeOffset(Now).ToUnixTimeSeconds().ToString();
        var v1 = MercadoPagoSignature.Sign(Secret, "req-1", "123456", ts);
        MercadoPagoSignature.IsValid(Secret, $"ts={ts},v1={v1}", "req-1", "123456", Now, TimeSpan.FromMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public void Tampered_or_stale_signatures_are_rejected()
    {
        var ts = new DateTimeOffset(Now).ToUnixTimeSeconds().ToString();
        var v1 = MercadoPagoSignature.Sign(Secret, "req-1", "123456", ts);

        MercadoPagoSignature.IsValid(Secret, $"ts={ts},v1={v1}", "req-1", "999999", Now).Should().BeFalse();
        MercadoPagoSignature.IsValid("otro", $"ts={ts},v1={v1}", "req-1", "123456", Now).Should().BeFalse();
        MercadoPagoSignature.IsValid(Secret, $"ts={ts},v1={v1}", "req-1", "123456", Now.AddHours(2), TimeSpan.FromMinutes(30)).Should().BeFalse();
        MercadoPagoSignature.IsValid(Secret, null, "req-1", "123456", Now).Should().BeFalse();
    }
}

public class FiscalRulesTests
{
    [Fact]
    public void Vat_is_extracted_from_final_price()
    {
        var (net, vat) = FiscalService.SplitVat(12100m);
        net.Should().Be(10000m);
        vat.Should().Be(2100m);
        (net + vat).Should().Be(12100m);
    }

    [Theory]
    [InlineData(CustomerTaxCondition.ResponsableInscripto, CustomerTaxCondition.ResponsableInscripto, CustomerDocumentType.Cuit, FiscalVoucherType.FacturaA)]
    [InlineData(CustomerTaxCondition.ResponsableInscripto, CustomerTaxCondition.ConsumidorFinal, CustomerDocumentType.Dni, FiscalVoucherType.FacturaB)]
    [InlineData(CustomerTaxCondition.ResponsableInscripto, CustomerTaxCondition.Monotributo, CustomerDocumentType.Cuit, FiscalVoucherType.FacturaB)]
    [InlineData(CustomerTaxCondition.Monotributo, CustomerTaxCondition.ResponsableInscripto, CustomerDocumentType.Cuit, FiscalVoucherType.FacturaC)]
    public void Voucher_type_depends_on_both_tax_conditions(CustomerTaxCondition emitter, CustomerTaxCondition receiver, CustomerDocumentType doc, FiscalVoucherType expected)
        => FiscalService.ResolveType(emitter, receiver, doc).Should().Be(expected);

    [Fact]
    public void Credit_notes_and_codes()
    {
        FiscalService.CreditNoteFor(FiscalVoucherType.FacturaB).Should().Be(FiscalVoucherType.NotaCreditoB);
        FiscalService.Letter(FiscalVoucherType.NotaCreditoA).Should().Be("A");
        FiscalService.ArcaDocType(CustomerDocumentType.Dni).Should().Be(96);
        FiscalService.ArcaDocType(CustomerDocumentType.None).Should().Be(99);
        FiscalService.ArcaIvaCondition(CustomerTaxCondition.ConsumidorFinal).Should().Be(5);
    }
}

public class MessageRenderingTests
{
    [Fact]
    public void Tokens_are_replaced_and_unknown_ones_kept()
    {
        var text = RenderOrderMessageService.ReplaceTokens("Hola {{customer_first_name}}, saldo {{balance_due}} {{nope}}",
            new Dictionary<string, string> { ["customer_first_name"] = "Ana", ["balance_due"] = "$ 1.500,00" });
        text.Should().Be("Hola Ana, saldo $ 1.500,00 {{nope}}");
    }

    [Theory]
    [InlineData(1234.5, "1.234,50")]
    [InlineData(1000000, "1.000.000,00")]
    [InlineData(0.5, "0,50")]
    public void Amounts_use_argentine_format(decimal value, string expected) => RenderOrderMessageService.Format(value).Should().Be(expected);

    [Fact]
    public void Whatsapp_link_encodes_text()
    {
        var link = RenderOrderMessageService.WhatsAppLink("5491123456789", "Hola Ana & cía");
        link.Should().StartWith("https://wa.me/5491123456789?text=").And.Contain("Hola%20Ana%20%26%20c%C3%ADa");
    }
}

public class SecurityRulesTests
{
    [Theory]
    [InlineData("corta1", false)]
    [InlineData("sololetrasaaaa", false)]
    [InlineData("12345678901", false)]
    [InlineData("Segura2026", true)]
    public void Password_policy(string password, bool ok)
    {
        var act = () => PasswordPolicy.Validate(password);
        if (ok) act.Should().NotThrow(); else act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Tokens_are_random_and_only_hashes_are_stored()
    {
        var a = TokenHasher.NewToken();
        var b = TokenHasher.NewToken();
        a.Should().NotBe(b);
        TokenHasher.Hash(a).Should().Be(TokenHasher.Hash(a)).And.NotBe(a).And.HaveLength(64);
    }

    [Fact]
    public void Permissions_by_role()
    {
        Permissions.For(UserRole.Admin).Should().Contain(Permissions.Admin);
        Permissions.For(UserRole.Cashier).Should().Equal(Permissions.Sales);
        Permissions.For(UserRole.Tech).Should().NotContain(Permissions.Sales);
        Permissions.For(UserRole.Reception).Should().Contain(new[] { Permissions.OrdersManage, Permissions.Sales });
    }
}

public class FileAndImportTests
{
    [Fact]
    public void File_type_is_detected_from_bytes_not_name()
    {
        FileService.Sniff(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }).ContentType.Should().Be("image/png");
        FileService.Sniff(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }).ContentType.Should().Be("image/jpeg");
        FileService.Sniff("%PDF-1.7"u8).ContentType.Should().Be("application/pdf");
        FileService.Sniff("<html><script>"u8).ContentType.Should().BeNull();
    }

    [Theory]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("$ 1.234", "1234")]
    [InlineData("12,50", "12.50")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("", null)]
    public void Numbers_from_spreadsheets(string input, string? expected) => ImportService.NormalizeNumber(input).Should().Be(expected);
}

public class CashSummaryTests
{
    [Fact]
    public void Expected_cash_includes_opening_float_and_declared_differences()
    {
        var now = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        var user = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var session = new CashRegisterSession(shop, 1, "ARS", 5000, null, user, now);
        var movements = new List<CashMovement>
        {
            new(shop, session.Id, CashMovementType.Sale, PaymentMethod.Cash, 3000, "ARS", "Venta V-000001", null, "sale", Guid.NewGuid(), user, now),
            new(shop, session.Id, CashMovementType.OrderPayment, PaymentMethod.Card, 10000, "ARS", "Pago #000010", null, "repair_order", Guid.NewGuid(), user, now),
            new(shop, session.Id, CashMovementType.Expense, PaymentMethod.Cash, 1200, "ARS", "Artículos de limpieza", "insumos", null, null, user, now),
            new(shop, session.Id, CashMovementType.OrderPayment, PaymentMethod.Cash, 50, "USD", "Seña en dólares", null, "repair_order", Guid.NewGuid(), user, now),
        };

        var lines = CashRegisterService.BuildSummary(session, movements, countedCash: 6700,
            declared: new[] { new DeclaredAmount(PaymentMethod.Card, "ARS", 10000), new DeclaredAmount(PaymentMethod.Cash, "USD", 50) });

        var cash = lines.Single(l => l.Currency == "ARS" && l.Method == "Cash");
        cash.Expected.Should().Be(5000 + 3000 - 1200);
        cash.Difference.Should().Be(-100);
        lines.Single(l => l.Method == "Card").Difference.Should().Be(0);
        lines.Single(l => l.Currency == "USD").Expected.Should().Be(50);
    }
}

public class OrderFinancialsTests
{
    [Fact]
    public void Total_is_agreed_price_plus_extra_parts_minus_payments()
    {
        var now = DateTime.UtcNow;
        var order = new RepairOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Cambio de batería", null, now);
        order.SetQuote(20000, "ARS", Guid.NewGuid(), now);
        var info = new OrderReadInfo(order.Id, "Ana", "11", null, true, "Apple", "iPhone", null, null, null, null,
            Paid: 5000, PaymentsCurrency: "ARS", ExtraCharges: 3500, HasApprovedQuote: true, HasOpenQuote: false, QaPassed: false, PhotosCount: 0);

        var money = OrderMapping.Financials(order, info, "ARS");
        money.Total.Should().Be(23500);
        money.BalanceDue.Should().Be(18500);
        money.HasAgreedPrice.Should().BeTrue();
    }
}
