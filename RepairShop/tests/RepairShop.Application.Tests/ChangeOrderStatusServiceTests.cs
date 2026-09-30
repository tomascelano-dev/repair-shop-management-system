using FluentAssertions;
using Moq;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Tests;

public class ChangeOrderStatusServiceTests
{
    [Fact]
    public async Task When_order_missing_should_throw_not_found()
    {
        var shopId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var orders = new Mock<IRepairOrderRepository>();
        var history = new Mock<IRepairOrderStatusHistoryRepository>();
        var audit = new Mock<IAuditEventRepository>();
        var outbox = new Mock<INotificationOutboxRepository>();
        var customers = new Mock<ICustomerRepository>();
        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IDateTimeProvider>();

        // Renderer deps (we force template missing so it falls back and doesn't hit other repos)
        var shops = new Mock<IShopRepository>();
        var templates = new Mock<IMessageTemplateRepository>();
        var devices = new Mock<IDeviceRepository>();
        var payments = new Mock<IRepairOrderPaymentRepository>();
        var checklists = new Mock<IRepairOrderReceptionChecklistRepository>();

        templates
            .Setup(x => x.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepairShop.Domain.Messaging.MessageTemplate?)null);

        var renderer = new RenderOrderMessageService(
            shops.Object,
            templates.Object,
            orders.Object,
            customers.Object,
            devices.Object,
            payments.Object,
            checklists.Object);

        orders.Setup(x => x.GetByIdAsync(shopId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepairOrder?)null);

        var svc = new ChangeOrderStatusService(
            orders.Object,
            history.Object,
            audit.Object,
            outbox.Object,
            customers.Object,
            uow.Object,
            clock.Object,
            renderer, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChangeOrderStatusService>.Instance);

        Func<Task> act = async () => await svc.HandleAsync(
            shopId,
            orderId,
            RepairOrderStatus.Diagnosing,
            actorUserId: Guid.NewGuid(),
            actorEmail: null,
            enqueueOutbox: false,
            channel: NotificationChannel.WhatsApp,
            ct: default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_change_status_add_history_and_save()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var shopId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var order = new RepairOrder(shopId, customerId, deviceId, "No carga", null, now);

        var orders = new Mock<IRepairOrderRepository>();
        var history = new Mock<IRepairOrderStatusHistoryRepository>();
        var audit = new Mock<IAuditEventRepository>();
        var outbox = new Mock<INotificationOutboxRepository>();
        var customers = new Mock<ICustomerRepository>();
        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IDateTimeProvider>();

        // Renderer deps (force fallback)
        var shops = new Mock<IShopRepository>();
        var templates = new Mock<IMessageTemplateRepository>();
        var devices = new Mock<IDeviceRepository>();
        var payments = new Mock<IRepairOrderPaymentRepository>();
        var checklists = new Mock<IRepairOrderReceptionChecklistRepository>();

        templates
            .Setup(x => x.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepairShop.Domain.Messaging.MessageTemplate?)null);

        var renderer = new RenderOrderMessageService(
            shops.Object,
            templates.Object,
            orders.Object,
            customers.Object,
            devices.Object,
            payments.Object,
            checklists.Object);

        clock.SetupGet(x => x.UtcNow).Returns(now.AddMinutes(10));

        orders.Setup(x => x.GetByIdAsync(shopId, order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var svc = new ChangeOrderStatusService(
            orders.Object,
            history.Object,
            audit.Object,
            outbox.Object,
            customers.Object,
            uow.Object,
            clock.Object,
            renderer, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChangeOrderStatusService>.Instance);

        await svc.HandleAsync(
            shopId,
            order.Id,
            RepairOrderStatus.Diagnosing,
            actorUserId: Guid.NewGuid(),
            actorEmail: null,
            enqueueOutbox: false,
            channel: NotificationChannel.WhatsApp,
            ct: default);

        order.Status.Should().Be(RepairOrderStatus.Diagnosing);
        history.Verify(x => x.AddAsync(It.IsAny<RepairOrderStatusHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
