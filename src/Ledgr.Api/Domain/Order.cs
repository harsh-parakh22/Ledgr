namespace Ledgr.Api.Domain;

public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;

    public decimal TotalAmount { get; set; }

    public int Version { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAtUtc { get; set; }

    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
}
