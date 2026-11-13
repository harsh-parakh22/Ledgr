namespace Ledgr.Api.Exceptions;

/// <summary>
/// A request that was understood but cannot be carried out. Carries the HTTP status the
/// API should return; anything not deriving from this type is treated as a server fault.
/// </summary>
public class LedgrException : Exception
{
    public LedgrException(string message, int statusCode = 400, string title = "Request rejected")
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

public sealed class ProductNotFoundException : LedgrException
{
    public ProductNotFoundException(int productId)
        : base($"Product {productId} was not found.", 404, "Product not found")
    {
    }
}

public sealed class OrderNotFoundException : LedgrException
{
    public OrderNotFoundException(int orderId)
        : base($"Order {orderId} was not found.", 404, "Order not found")
    {
    }
}

public sealed class InsufficientStockException : LedgrException
{
    public InsufficientStockException(string sku, int requested, int available)
        : base($"Only {available} unit(s) of {sku} are in stock; {requested} were requested.", 409, "Insufficient stock")
    {
    }
}

public sealed class InactiveProductException : LedgrException
{
    public InactiveProductException(string sku)
        : base($"Product {sku} is no longer active and cannot be ordered.", 409, "Product is inactive")
    {
    }
}

public sealed class DuplicateSkuException : LedgrException
{
    public DuplicateSkuException(string sku)
        : base($"A product with SKU {sku} already exists.", 409, "Duplicate SKU")
    {
    }
}

public sealed class StockConflictException : LedgrException
{
    public StockConflictException()
        : base("Stock changed while this request was being processed. Retry it.", 409, "Stock conflict")
    {
    }
}
