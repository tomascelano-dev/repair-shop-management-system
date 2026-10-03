namespace RepairShop.Domain.RepairOrders;

// NOTE: values are persisted as integers. Never renumber.
public enum PaymentMethod
{
    Cash = 0,
    Transfer = 1,
    Card = 2,
    MercadoPago = 3,
    Other = 99
}
