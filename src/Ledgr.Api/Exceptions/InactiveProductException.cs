namespace Ledgr.Api.Exceptions;

public sealed class InactiveProductException : LedgrException
{
    public InactiveProductException(string sku)
        : base($"Product {sku} is no longer active and cannot be ordered.", 409, "Product is inactive")
    {
    }
}

