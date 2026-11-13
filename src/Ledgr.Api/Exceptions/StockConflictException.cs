namespace Ledgr.Api.Exceptions;

public sealed class StockConflictException : LedgrException
{
    public StockConflictException()
        : base("Stock changed while this request was being processed. Retry it.", 409, "Stock conflict")
    {
    }
}

