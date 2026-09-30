using FluentAssertions;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Domain.Tests;

public class RepairOrderStatusTests
{
    [Fact]
    public void Invalid_transition_should_throw()
    {
        var now = DateTime.UtcNow;
        var order = new RepairOrder(Guid.NewGuid(), Guid.NewGuid(), "No carga", null, now);

        var act = () => order.MoveTo(RepairOrderStatus.InProgress, now.AddMinutes(1)); // skipping Diagnosing
        act.Should().Throw<DomainException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public void Happy_path_transitions_should_work()
    {
        var now = DateTime.UtcNow;
        var order = new RepairOrder(Guid.NewGuid(), Guid.NewGuid(), "Pantalla rota", null, now);

        order.Status.Should().Be(RepairOrderStatus.Received);

        order.MoveTo(RepairOrderStatus.Diagnosing, now.AddMinutes(1));
        order.MoveTo(RepairOrderStatus.InProgress, now.AddMinutes(2));
        order.MoveTo(RepairOrderStatus.Ready, now.AddMinutes(3));
        order.MoveTo(RepairOrderStatus.Delivered, now.AddMinutes(4));

        order.Status.Should().Be(RepairOrderStatus.Delivered);
    }

    [Fact]
    public void Cannot_change_status_after_final()
    {
        var now = DateTime.UtcNow;
        var order = new RepairOrder(Guid.NewGuid(), Guid.NewGuid(), "Pantalla rota", null, now);

        order.MoveTo(RepairOrderStatus.Diagnosing, now.AddMinutes(1));
        order.MoveTo(RepairOrderStatus.InProgress, now.AddMinutes(2));
        order.MoveTo(RepairOrderStatus.Ready, now.AddMinutes(3));
        order.MoveTo(RepairOrderStatus.Delivered, now.AddMinutes(4));

        var act = () => order.MoveTo(RepairOrderStatus.Cancelled, now.AddMinutes(5));
        act.Should().Throw<DomainException>()
            .WithMessage("*Finalized orders cannot change status*");
    }
}
