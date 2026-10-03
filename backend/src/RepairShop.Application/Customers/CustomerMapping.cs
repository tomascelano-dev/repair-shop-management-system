using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;

namespace RepairShop.Application.Customers;

public static class CustomerMapping
{
    public static CustomerResponse ToResponse(this Customer c, string phoneCountryCode = "54")
        => new(c.Id, c.ShopId, c.FullName, c.Phone, c.Notes, c.CreatedAtUtc, c.Email, c.DocumentType, c.DocumentNumber, c.TaxCondition,
            c.Address, c.Tags, c.NotificationsOptIn, c.MarketingOptIn, c.UpdatedAtUtc, PhoneNumber.ToWhatsAppDigits(c.Phone, phoneCountryCode));

    public static DeviceResponse ToResponse(this Device d, string? customerName = null)
        => new(d.Id, d.ShopId, d.CustomerId, d.Brand, d.Model, d.Label, d.SerialNumber, d.Notes, d.CreatedAtUtc, d.Imei, d.UpdatedAtUtc, customerName);
}
