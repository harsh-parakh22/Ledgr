using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Models;

[Index(nameof(Sku), IsUnique = true)]
public class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Category { get; set; } = "General";

    [Range(0.01, 1000000.00)]
    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    public int QuantityOnHand { get; set; }

    public int ReorderLevel { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

