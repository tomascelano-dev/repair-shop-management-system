namespace RepairShop.Domain.RepairOrders;

// NOTE: values are persisted as integers. Never renumber; append new values at the end.
public enum RepairOrderStatus
{
    Received = 0,
    Diagnosing = 1,
    InProgress = 2,
    Ready = 3,
    Delivered = 4,
    Cancelled = 5,
    WaitingParts = 6,
    Testing = 7
}

public enum RepairOrderPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum UnlockMethod
{
    None = 0,
    Pin = 1,
    Password = 2,
    Pattern = 3
}
