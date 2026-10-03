namespace RepairShop.Domain.Inventory;

// NOTE: values are persisted as integers. Never renumber; append new values at the end.
public enum InventoryAdjustmentType
{
    Manual = 0,
    Purchase = 1,
    Sale = 2,
    Consumption = 3,
    Correction = 4,
    Return = 5,
    TransferOut = 6,
    TransferIn = 7
}
