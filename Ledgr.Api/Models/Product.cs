namespace Ledgr.Api.Models;

public class Product
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int QuantityOnHand { get; private set; }

    public int ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<InventoryMovement> Movements { get; } = new List<InventoryMovement>();

    public ICollection<OrderItem> OrderItems { get; } = new List<OrderItem>();

    public bool IsLowStock => QuantityOnHand <= ReorderLevel;

    /// <summary>
    /// Changes stock and records the matching ledger entry. Every quantity change in the
    /// application goes through here, so on-hand stock and its history cannot drift apart.
    /// </summary>
    public InventoryMovement ApplyMovement(int quantityChange, MovementType type, string reason, string? referenceId)
    {
        var movement = InventoryMovement.Create(this, quantityChange, type, reason, referenceId);

        QuantityOnHand = movement.NewQuantity;
        UpdatedAtUtc = movement.CreatedAtUtc;
        Movements.Add(movement);

        return movement;
    }
}
