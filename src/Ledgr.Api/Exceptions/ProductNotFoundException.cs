namespace Ledgr.Api.Exceptions;

public sealed class ProductNotFoundException : LedgrException
{
    public ProductNotFoundException(int productId)
        : base($"Product {productId} was not found.", 404, "Product not found")
    {
    }
}

