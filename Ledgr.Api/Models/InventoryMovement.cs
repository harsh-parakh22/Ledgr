using Ledgr.Api.Exceptions;

namespace Ledgr.Api.Models;

public class InventoryMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int QuantityChange { get; set; }

    public int PreviousQuantity { get; set; }

    public int NewQuantity { get; set; }

    public MovementType Type { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public static InventoryMovement Create(
        Product product,
        int quantityChange,
        MovementType type,
        string reason,
        string? referenceId)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantityChange == 0)
        {
            throw new LedgrException("A stock movement must change the quantity.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new LedgrException($"Unknown movement type '{(int)type}'.");
        }

        if (!AllowsSign(type, quantityChange))
        {
            throw new LedgrException($"{type} movements cannot carry a {(quantityChange < 0 ? "negative" : "positive")} quantity.");
        }

        var newQuantity = product.QuantityOnHand + quantityChange;
        if (newQuantity < 0)
        {
            throw new InsufficientStockException(product.Sku, -quantityChange, product.QuantityOnHand);
        }

        return new InventoryMovement
        {
            Product = product,
            ProductId = product.Id,
            QuantityChange = quantityChange,
            PreviousQuantity = product.QuantityOnHand,
            NewQuantity = newQuantity,
            Type = type,
            Reason = reason.Trim(),
            ReferenceId = string.IsNullOrWhiteSpace(referenceId) ? null : referenceId.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static bool AllowsSign(MovementType type, int quantityChange) => type switch
    {
        MovementType.Adjustment => true,
        MovementType.StockIn or MovementType.OrderCancel => quantityChange > 0,
        MovementType.OrderFulfill or MovementType.WriteOff => quantityChange < 0,
        _ => false
    };
}
