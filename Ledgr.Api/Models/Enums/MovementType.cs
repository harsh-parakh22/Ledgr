namespace Ledgr.Api.Models.Enums;

public enum MovementType
{
    StockIn = 1,        // Restock / Supplier PO received
    OrderFulfill = 2,   // Reserved and deducted for a customer order
    Adjustment = 3,     // Manual cycle count correction
    WriteOff = 4        // Damaged, expired, or lost goods
}

