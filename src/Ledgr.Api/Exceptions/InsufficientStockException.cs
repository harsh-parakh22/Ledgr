namespace Ledgr.Api.Exceptions;

public sealed class InsufficientStockException : LedgrException
{
    public InsufficientStockException(string sku, int requested, int available)
        : base($"Only {available} unit(s) of {sku} are in stock; {requested} were requested.", 409, "Insufficient stock")
    {
    }
}

