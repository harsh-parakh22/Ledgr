namespace Ledgr.Api.Contracts;

public sealed record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

