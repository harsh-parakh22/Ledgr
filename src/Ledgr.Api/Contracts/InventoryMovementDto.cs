using Ledgr.Api.Domain;

namespace Ledgr.Api.Contracts;

public sealed record InventoryMovementDto(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    int QuantityChange,
    int PreviousQuantity,
    int NewQuantity,
    string Type,
    string Reason,
    string? ReferenceId,
    DateTime CreatedAtUtc)
{
    public static InventoryMovementDto From(InventoryMovement movement, Product product) => new(
        movement.Id,
        movement.ProductId,
        product.Name,
        product.Sku,
        movement.QuantityChange,
        movement.PreviousQuantity,
        movement.NewQuantity,
        movement.Type.ToString(),
        movement.Reason,
        movement.ReferenceId,
        movement.CreatedAtUtc);
}

