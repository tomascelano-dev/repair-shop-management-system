using FluentAssertions;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Domain.Tests;

public class RepairOrderStatusTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StatusTransitionContext Approved = new(HasApprovedQuote: true);
    private static readonly StatusTransitionContext QaOk = new(QaPassed: true);

    private static RepairOrder NewOrder() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Pantalla rota", null, T0);

    private static RepairOrder ReadyOrder()
    {
        var o = NewOrder();
        o.MoveTo(RepairOrderStatus.Diagnosing, T0.AddMinutes(1));
        o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(2), Approved);
        o.MoveTo(RepairOrderStatus.Ready, T0.AddMinutes(3), QaOk);
        return o;
    }

    [Fact]
    public void New_order_starts_received()
    {
        var o = NewOrder();
        o.Status.Should().Be(RepairOrderStatus.Received);
        o.LastStatusChangeAtUtc.Should().Be(T0);
        o.PublicToken.Should().HaveLength(32);
    }

    [Fact]
    public void Skipping_diagnosis_is_not_allowed()
    {
        var act = () => NewOrder().MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(1), Approved);
        act.Should().Throw<DomainException>().WithMessage("*Transición de estado inválida*");
    }

    [Theory]
    [InlineData(RepairOrderStatus.InProgress)]
    [InlineData(RepairOrderStatus.WaitingParts)]
    public void Repair_requires_an_approved_quote(RepairOrderStatus target)
    {
        var o = NewOrder();
        o.MoveTo(RepairOrderStatus.Diagnosing, T0.AddMinutes(1));

        var act = () => o.MoveTo(target, T0.AddMinutes(2));
        act.Should().Throw<DomainException>().WithMessage("*presupuesto*aprobado*");

        o.MoveTo(target, T0.AddMinutes(3), Approved);
        o.Status.Should().Be(target);
    }

    [Fact]
    public void Warranty_claims_do_not_need_a_quote()
    {
        var o = NewOrder();
        o.MarkAsWarrantyClaim(Guid.NewGuid(), "ARS", T0);
        o.MoveTo(RepairOrderStatus.Diagnosing, T0.AddMinutes(1));
        o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(2));
        o.Status.Should().Be(RepairOrderStatus.InProgress);
        o.QuoteAmount.Should().Be(0m);
    }

    [Fact]
    public void Ready_requires_quality_control()
    {
        var o = NewOrder();
        o.MoveTo(RepairOrderStatus.Diagnosing, T0.AddMinutes(1));
        o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(2), Approved);

        var act = () => o.MoveTo(RepairOrderStatus.Ready, T0.AddMinutes(3));
        act.Should().Throw<DomainException>().WithMessage("*control de calidad*");

        o.MoveTo(RepairOrderStatus.Ready, T0.AddMinutes(4), QaOk);
        o.ReadyAtUtc.Should().Be(T0.AddMinutes(4));
    }

    [Fact]
    public void Waiting_parts_and_testing_flow()
    {
        var o = NewOrder();
        o.MoveTo(RepairOrderStatus.Diagnosing, T0.AddMinutes(1));
        o.MoveTo(RepairOrderStatus.WaitingParts, T0.AddMinutes(2), Approved);
        o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(3));
        o.MoveTo(RepairOrderStatus.Testing, T0.AddMinutes(4));
        o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(5));
        o.MoveTo(RepairOrderStatus.Testing, T0.AddMinutes(6));
        o.MoveTo(RepairOrderStatus.Ready, T0.AddMinutes(7), QaOk);
        o.Status.Should().Be(RepairOrderStatus.Ready);
    }

    [Fact]
    public void Ready_can_go_back_to_rework_and_clears_ready_date()
    {
        var o = ReadyOrder();
        o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(10));
        o.ReadyAtUtc.Should().BeNull();
        o.Status.Should().Be(RepairOrderStatus.InProgress);
    }

    [Fact]
    public void Delivery_with_balance_due_is_blocked_unless_forced()
    {
        var o = ReadyOrder();
        var act = () => o.MoveTo(RepairOrderStatus.Delivered, T0.AddMinutes(10), new StatusTransitionContext(BalanceDue: 1500m));
        act.Should().Throw<DomainException>().WithMessage("*saldo pendiente*");

        o.MoveTo(RepairOrderStatus.Delivered, T0.AddMinutes(11), new StatusTransitionContext(BalanceDue: 1500m, AllowUnpaidDelivery: true));
        o.Status.Should().Be(RepairOrderStatus.Delivered);
    }

    [Fact]
    public void Delivery_starts_warranty_and_purges_unlock_code()
    {
        var o = ReadyOrder();
        o.SetUnlockSecret(UnlockMethod.Pin, "encrypted-pin", T0.AddMinutes(5));
        o.UnlockSecretProtected.Should().NotBeNull();

        o.MoveTo(RepairOrderStatus.Delivered, T0.AddDays(1), new StatusTransitionContext(DefaultWarrantyDays: 90));

        o.DeliveredAtUtc.Should().Be(T0.AddDays(1));
        o.WarrantyDays.Should().Be(90);
        o.WarrantyExpiresAtUtc.Should().Be(T0.AddDays(91));
        o.IsUnderWarranty(T0.AddDays(30)).Should().BeTrue();
        o.IsUnderWarranty(T0.AddDays(92)).Should().BeFalse();
        o.UnlockSecretProtected.Should().BeNull();
        o.UnlockSecretPurgedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Order_specific_warranty_wins_over_shop_default()
    {
        var o = ReadyOrder();
        o.SetWarrantyDays(30, T0);
        o.MoveTo(RepairOrderStatus.Delivered, T0.AddDays(1), new StatusTransitionContext(DefaultWarrantyDays: 90));
        o.WarrantyExpiresAtUtc.Should().Be(T0.AddDays(31));
    }

    [Fact]
    public void Cancellation_keeps_the_reason()
    {
        var o = NewOrder();
        o.MoveTo(RepairOrderStatus.Cancelled, T0.AddMinutes(1), new StatusTransitionContext(Reason: "  El cliente no aceptó  "));
        o.CancellationReason.Should().Be("El cliente no aceptó");
        o.CancelledAtUtc.Should().Be(T0.AddMinutes(1));
    }

    [Theory]
    [InlineData(RepairOrderStatus.Delivered)]
    [InlineData(RepairOrderStatus.Cancelled)]
    public void Final_orders_cannot_change(RepairOrderStatus final)
    {
        var o = ReadyOrder();
        o.MoveTo(final, T0.AddMinutes(20));

        var act = () => o.MoveTo(RepairOrderStatus.InProgress, T0.AddMinutes(21));
        act.Should().Throw<DomainException>().WithMessage("*finalizadas*");
        RepairOrder.AllowedTransitions(final).Should().BeEmpty();
    }

    [Fact]
    public void Overdue_only_for_open_orders_past_promised_date()
    {
        var o = NewOrder();
        o.Plan(null, RepairOrderPriority.Urgent, T0.AddDays(1), T0);
        o.IsOverdue(T0.AddHours(12)).Should().BeFalse();
        o.IsOverdue(T0.AddDays(2)).Should().BeTrue();
        o.Priority.Should().Be(RepairOrderPriority.Urgent);
    }

    [Fact]
    public void Order_code_is_padded_order_number()
    {
        var o = NewOrder();
        o.Code.Should().Be("#------");
        o.AssignNumber(123);
        o.Code.Should().Be("#000123");
        var act = () => o.AssignNumber(124);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Regenerating_tracking_token_invalidates_the_old_one()
    {
        var o = NewOrder();
        var before = o.PublicToken;
        o.RegenerateToken(T0.AddMinutes(1));
        o.PublicToken.Should().NotBe(before);
    }
}
