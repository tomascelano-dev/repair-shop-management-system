namespace RepairShop.Domain.Notifications;

public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}
