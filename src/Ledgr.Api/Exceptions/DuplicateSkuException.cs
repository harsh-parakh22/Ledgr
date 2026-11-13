namespace Ledgr.Api.Exceptions;

public sealed class DuplicateSkuException : LedgrException
{
    public DuplicateSkuException(string sku)
        : base($"A product with SKU {sku} already exists.", 409, "Duplicate SKU")
    {
    }
}

