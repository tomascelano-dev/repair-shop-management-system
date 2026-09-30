using FluentAssertions;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Domain.Tests;

public class EntityInvariantTests
{
    [Fact]
    public void Customer_requires_valid_name_and_phone()
    {
        var now = DateTime.UtcNow;

        Action act1 = () => new Customer("ab", "123456", null, now);
        act1.Should().Throw<DomainException>();

        Action act2 = () => new Customer("Juan Perez", "123", null, now);
        act2.Should().Throw<DomainException>();
    }

    [Fact]
    public void Device_requires_customer_and_brand_model()
    {
        var now = DateTime.UtcNow;

        Action act1 = () => new Device(Guid.Empty, "Apple", "iPhone", null, null, now);
        act1.Should().Throw<DomainException>();

        Action act2 = () => new Device(Guid.NewGuid(), "A", "iPhone", null, null, now);
        act2.Should().Throw<DomainException>();
    }

    [Fact]
    public void Order_note_and_attachment_validate_input()
    {
        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Action act1 = () => new RepairOrderNote(orderId, " ", userId, now);
        act1.Should().Throw<DomainException>();

        Action act2 = () => new RepairOrderAttachment(orderId, "ftp://bad", null, userId, now);
        act2.Should().Throw<DomainException>();
    }
}
