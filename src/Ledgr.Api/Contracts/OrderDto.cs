namespace Ledgr.Api.Contracts;

public sealed record OrderDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerEmail,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyList<OrderItemDto> Items);

