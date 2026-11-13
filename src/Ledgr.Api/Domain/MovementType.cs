namespace Ledgr.Api.Domain;

public enum MovementType
{
    StockIn = 1,
    OrderFulfill = 2,
    Adjustment = 3,
    WriteOff = 4,
    OrderCancel = 5
}
