using FluentAssertions;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;

namespace RepairShop.Domain.Tests;

public class EntityInvariantTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Customer_requires_valid_name_and_phone()
    {
        var shop = Guid.NewGuid();
        ((Action)(() => new Customer(shop, "ab", "123456", null, Now))).Should().Throw<DomainException>();
        ((Action)(() => new Customer(shop, "Juan Perez", "123", null, Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Customer_phone_key_ignores_formatting()
    {
        var a = new Customer(Guid.NewGuid(), "Juan Perez", "011 15 2345-6789", null, Now);
        var b = new Customer(Guid.NewGuid(), "Juan Perez", "+54 9 11 2345 6789", null, Now);
        a.PhoneKey.Should().Be("23456789").And.Be(b.PhoneKey);
    }

    [Fact]
    public void Customer_fiscal_data_is_validated()
    {
        var c = new Customer(Guid.NewGuid(), "Empresa SA", "1144445555", null, Now);
        c.UpdateFiscal(CustomerDocumentType.Cuit, "20-12345678-6", CustomerTaxCondition.ResponsableInscripto, Now);
        c.DocumentNumber.Should().Be("20123456786");

        var act = () => c.UpdateFiscal(CustomerDocumentType.Cuit, "20-12345678-0", CustomerTaxCondition.ResponsableInscripto, Now);
        act.Should().Throw<DomainException>().WithMessage("*CUIT*");
    }

    [Fact]
    public void Customer_contact_and_tags_are_normalized()
    {
        var c = new Customer(Guid.NewGuid(), "Juan Perez", "1144445555", null, Now);
        c.UpdateContact(" Juan@Mail.COM ", null, "VIP, empresa,vip ", Now);
        c.Email.Should().Be("juan@mail.com");
        c.Tags.Should().Be("vip,empresa");
        ((Action)(() => c.UpdateContact("no-es-email", null, null, Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Device_requires_customer_and_brand_model()
    {
        ((Action)(() => new Device(Guid.NewGuid(), Guid.Empty, "Apple", "iPhone", null, null, null, Now))).Should().Throw<DomainException>();
        ((Action)(() => new Device(Guid.NewGuid(), Guid.NewGuid(), "A", "iPhone", null, null, null, Now))).Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("490154203237518", true)]
    [InlineData("356938035643809", true)]
    [InlineData("490154203237519", false)]
    [InlineData("12345", false)]
    [InlineData("49015420323751A", false)]
    public void Imei_uses_luhn_check_digit(string imei, bool valid)
        => ImeiValidator.IsValid(imei).Should().Be(valid);

    [Fact]
    public void Device_imei_is_stored_as_digits()
    {
        var d = new Device(Guid.NewGuid(), Guid.NewGuid(), "Apple", "iPhone 12", null, null, null, Now);
        d.SetImei("49-015420-323751-8", Now);
        d.Imei.Should().Be("490154203237518");
        ((Action)(() => d.SetImei("490154203237519", Now))).Should().Throw<DomainException>().WithMessage("*IMEI*");
    }

    [Fact]
    public void Order_note_and_attachment_validate_input()
    {
        var shop = Guid.NewGuid();
        var order = Guid.NewGuid();
        var user = Guid.NewGuid();
        ((Action)(() => new RepairOrderNote(shop, order, " ", user, Now))).Should().Throw<DomainException>();
        ((Action)(() => new RepairOrderAttachment(shop, order, "ftp://bad", null, user, Now))).Should().Throw<DomainException>();
        new RepairOrderNote(shop, order, "Visible para el cliente", true, user, Now).IsPublic.Should().BeTrue();
    }

    [Theory]
    [InlineData("20123456786", true)]
    [InlineData("30500010912", true)]
    [InlineData("20123456780", false)]
    [InlineData("2012345678", false)]
    public void Cuit_check_digit(string cuit, bool valid) => Customer.IsValidCuit(cuit).Should().Be(valid);

    [Fact]
    public void Shop_settings_are_validated_and_normalized()
    {
        var shop = new Shop("Mi Taller", null, null, null, "AR", Now);
        shop.OrganizationId.Should().Be(shop.Id);

        shop.UpdateOperations(90, 7, null, null, null, "https://g.page/r/abc", "30, 7,15,7", 5, true, Now);
        shop.ReadyReminderDays.Should().Be("7,15,30");
        shop.GetReadyReminderDays().Should().Equal(7, 15, 30);

        ((Action)(() => shop.UpdateOperations(90, 7, null, null, null, "http://inseguro", null, 5, true, Now))).Should().Throw<DomainException>();
        ((Action)(() => shop.UpdateOperations(-1, 7, null, null, null, null, null, 5, true, Now))).Should().Throw<DomainException>();
        ((Action)(() => shop.UpdateBusiness("Mi Taller SRL", "20-12345678-0", CustomerTaxCondition.Monotributo, null, Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Branch_shares_organization_of_parent()
    {
        var parent = new Shop("Casa central", null, null, null, "AR", Now);
        var branch = new Shop(parent.OrganizationId, "Sucursal Norte", null, null, null, "AR", Now);
        branch.OrganizationId.Should().Be(parent.OrganizationId);
    }
}
