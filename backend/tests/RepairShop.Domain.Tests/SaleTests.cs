using FluentAssertions;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Sales;

namespace RepairShop.Domain.Tests;

public class SaleTests
{
    private static readonly DateTime T0 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid User = Guid.NewGuid();

    private static SaleLineInput Line(decimal price, int qty = 1, decimal discount = 0)
        => new(Guid.NewGuid(), "SKU", "Producto", qty, price, discount, price / 2, true, 30);

    private static Sale NewSale(IReadOnlyCollection<SaleLineInput> lines, decimal discount, params SalePaymentInput[] payments)
        => new(Guid.NewGuid(), 1, null, "ARS", lines, discount, payments, Guid.NewGuid(), null, User, T0);

    [Fact]
    public void Cash_payment_gives_change()
    {
        var s = NewSale(new[] { Line(4500), Line(500, 2) }, 0, new SalePaymentInput(PaymentMethod.Cash, 10000, null));
        s.Total.Should().Be(5500);
        s.ChangeAmount.Should().Be(4500);
        s.NetPaymentsByMethod()[PaymentMethod.Cash].Should().Be(5500);
        s.Code.Should().Be("V-000001");
    }

    [Fact]
    public void Split_payment_must_cover_total()
    {
        var act = () => NewSale(new[] { Line(5000) }, 0, new SalePaymentInput(PaymentMethod.Card, 3000, null), new SalePaymentInput(PaymentMethod.Cash, 1000, null));
        act.Should().Throw<DomainException>().WithMessage("*Falta cobrar*");

        var ok = NewSale(new[] { Line(5000) }, 0, new SalePaymentInput(PaymentMethod.Card, 3000, null), new SalePaymentInput(PaymentMethod.Cash, 2000, null));
        ok.ChangeAmount.Should().Be(0);
    }

    [Fact]
    public void Only_cash_can_generate_change()
    {
        var act = () => NewSale(new[] { Line(5000) }, 0, new SalePaymentInput(PaymentMethod.Card, 6000, null));
        act.Should().Throw<DomainException>().WithMessage("*efectivo*vuelto*");
    }

    [Fact]
    public void Line_and_global_discounts()
    {
        var s = NewSale(new[] { Line(1000, 2, discount: 200), Line(3000) }, 300, new SalePaymentInput(PaymentMethod.Transfer, 4500, null));
        s.Subtotal.Should().Be(4800);
        s.Total.Should().Be(4500);
    }

    [Fact]
    public void Partial_then_full_refund_returns_exact_total()
    {
        var s = NewSale(new[] { Line(1000, 3), Line(999.99m) }, 100, new SalePaymentInput(PaymentMethod.Cash, 5000, null));
        var first = s.Refund(new Dictionary<Guid, int> { [s.Lines[0].Id] = 1 }, PaymentMethod.Cash, true, "falla", User, null, T0.AddHours(1));
        s.Status.Should().Be(SaleStatus.PartiallyRefunded);
        first.Amount.Should().BeGreaterThan(0).And.BeLessThan(1000);

        s.Refund(new Dictionary<Guid, int> { [s.Lines[0].Id] = 2, [s.Lines[1].Id] = 1 }, PaymentMethod.Cash, true, null, User, null, T0.AddHours(2));
        s.Status.Should().Be(SaleStatus.Refunded);
        s.RefundedAmount.Should().Be(s.Total);
    }

    [Fact]
    public void Cannot_refund_more_than_sold()
    {
        var s = NewSale(new[] { Line(1000, 1) }, 0, new SalePaymentInput(PaymentMethod.Cash, 1000, null));
        var act = () => s.Refund(new Dictionary<Guid, int> { [s.Lines[0].Id] = 2 }, PaymentMethod.Cash, true, null, User, null, T0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Void_refunds_everything_and_blocks_further_changes()
    {
        var s = NewSale(new[] { Line(1000, 2) }, 0, new SalePaymentInput(PaymentMethod.Cash, 2000, null));
        var refund = s.Void("Error de carga", PaymentMethod.Cash, User, null, T0);
        refund.Amount.Should().Be(2000);
        s.Status.Should().Be(SaleStatus.Voided);
        ((Action)(() => s.Void("otra vez", PaymentMethod.Cash, User, null, T0))).Should().Throw<DomainException>();
    }
}
