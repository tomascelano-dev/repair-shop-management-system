using FluentAssertions;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Notifications;

namespace RepairShop.Domain.Tests;

public class InventoryAndCashTests
{
    private static readonly DateTime T0 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Stock_never_goes_negative()
    {
        var item = new InventoryItem(Guid.NewGuid(), "mod-ip11", "Módulo", 2, 100, "ARS", true, T0);
        item.Sku.Should().Be("MOD-IP11");
        item.ApplyDelta(-2, T0);
        item.QuantityOnHand.Should().Be(0);
        ((Action)(() => item.ApplyDelta(-1, T0))).Should().Throw<DomainException>().WithMessage("*Stock insuficiente*");
    }

    [Fact]
    public void Untracked_items_ignore_stock()
    {
        var service = new InventoryItem(Guid.NewGuid(), "SRV-1", "Instalación de templado", 0, null, null, true, T0);
        service.UpdateCatalog("Servicios", null, 0, trackStock: false, isSellable: true, 3000, "ARS", null, null, T0);
        service.ApplyDelta(-5, T0);
        service.QuantityOnHand.Should().Be(0);
        service.IsLowStock(0).Should().BeFalse();
    }

    [Fact]
    public void Purchases_update_weighted_average_cost()
    {
        var item = new InventoryItem(Guid.NewGuid(), "BAT-1", "Batería", 10, 1000, "ARS", true, T0);
        item.ReceivePurchase(10, 2000, "ARS", T0);
        item.QuantityOnHand.Should().Be(20);
        item.UnitCost.Should().Be(1500);
    }

    [Fact]
    public void Low_stock_considers_reservations()
    {
        var item = new InventoryItem(Guid.NewGuid(), "BAT-2", "Batería", 5, 1000, "ARS", true, T0);
        item.UpdateCatalog(null, null, 2, true, false, null, null, null, null, T0);
        item.IsLowStock(0).Should().BeFalse();
        item.IsLowStock(3).Should().BeTrue();
    }

    [Fact]
    public void Sellable_items_need_a_price()
    {
        var item = new InventoryItem(Guid.NewGuid(), "ACC-1", "Funda", 5, 1000, "ARS", true, T0);
        ((Action)(() => item.UpdateCatalog(null, null, 0, true, true, null, null, null, null, T0))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Reservation_is_consumed_partially_then_fully()
    {
        var r = new InventoryReservation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 3, T0);
        r.Consume(2, T0).Should().Be(2);
        r.RemainingQuantity.Should().Be(1);
        r.Consume(5, T0).Should().Be(1);
        r.Status.Should().Be(ReservationStatus.Consumed);
        r.Consume(1, T0).Should().Be(0);
    }

    [Fact]
    public void Released_reservation_frees_stock()
    {
        var r = new InventoryReservation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 2, T0);
        r.Release(T0);
        r.Status.Should().Be(ReservationStatus.Released);
        r.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public void Cash_close_records_difference()
    {
        var s = new CashRegisterSession(Guid.NewGuid(), 1, "ARS", 10000, null, Guid.NewGuid(), T0);
        s.Close(expectedCash: 25500, countedCash: 25000, "{}", "faltante", Guid.NewGuid(), T0.AddHours(8));
        s.Difference.Should().Be(-500);
        s.IsOpen.Should().BeFalse();
        ((Action)(() => s.EnsureOpen())).Should().Throw<DomainException>();
    }

    [Fact]
    public void Outbox_retries_with_backoff_then_gives_up()
    {
        var item = new NotificationOutboxItem(Guid.NewGuid(), NotificationChannel.WhatsApp, "5491123456789", "Aviso", "Tu equipo está listo",
            OutboxStatus.Pending, "k", "repair_order", Guid.NewGuid(), T0);

        item.RegisterFailure("timeout", permanent: false, T0);
        item.Status.Should().Be(OutboxStatus.Failed);
        item.NextAttemptAtUtc.Should().Be(T0.AddMinutes(1));

        item.RegisterFailure("timeout", permanent: false, T0);
        item.NextAttemptAtUtc.Should().Be(T0.AddMinutes(5));

        for (var i = 0; i < 10 && item.Status != OutboxStatus.Cancelled; i++) item.RegisterFailure("timeout", false, T0);
        item.Status.Should().Be(OutboxStatus.Cancelled);
        item.AttemptCount.Should().Be(NotificationOutboxItem.MaxAttempts);
    }

    [Fact]
    public void Permanent_failure_cancels_immediately_and_sent_messages_are_final()
    {
        var item = new NotificationOutboxItem(Guid.NewGuid(), NotificationChannel.Sms, "5491123456789", "Aviso", "Tu equipo está listo",
            OutboxStatus.Pending, "k2", "repair_order", Guid.NewGuid(), T0);
        item.RegisterFailure("número inválido", permanent: true, T0);
        item.Status.Should().Be(OutboxStatus.Cancelled);

        item.Retry(T0);
        item.MarkSent("simulated", "abc", T0);
        ((Action)(() => item.MarkCancelled(T0))).Should().Throw<DomainException>();
    }
}
