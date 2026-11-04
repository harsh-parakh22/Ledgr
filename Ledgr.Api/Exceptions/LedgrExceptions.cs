namespace Ledgr.Api.Exceptions;

public class LedgrException : Exception
{
    public int StatusCode { get; }

    public LedgrException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class ProductNotFoundException : LedgrException
{
    public ProductNotFoundException(int productId) 
        : base($"Product with ID '{productId}' was not found.", 404)
    {
    }
}

public class InsufficientStockException : LedgrException
{
    public InsufficientStockException(string sku, int requested, int available) 
        : base($"Insufficient stock for '{sku}'. Requested: {requested}, Available: {available}.", 409)
    {
    }
}

public class InactiveProductException : LedgrException
{
    public InactiveProductException(string sku) 
        : base($"Cannot process order for deactivated product '{sku}'.", 400)
    {
    }
}

public class DuplicateSkuException : LedgrException
{
    public DuplicateSkuException(string sku) 
        : base($"A product with SKU '{sku}' already exists.", 409)
    {
    }
}

