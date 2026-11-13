namespace Ledgr.Api.Exceptions;

public sealed class OrderNotFoundException : LedgrException
{
    public OrderNotFoundException(int orderId)
        : base($"Order {orderId} was not found.", 404, "Order not found")
    {
    }
}

