using Ledgr.Api.Domain;

namespace Ledgr.Api.Contracts;

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    string Category,
    decimal UnitPrice,
    int QuantityOnHand,
    int ReorderLevel,
    bool IsActive,
    bool IsLowStock,
    DateTime CreatedAtUtc)
{
    public static ProductDto From(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Category,
        product.UnitPrice,
        product.QuantityOnHand,
        product.ReorderLevel,
        product.IsActive,
        product.IsLowStock,
        product.CreatedAtUtc);
}

