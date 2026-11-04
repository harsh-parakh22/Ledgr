using System.ComponentModel.DataAnnotations;
using Ledgr.Api.Models.Enums;

namespace Ledgr.Api.Models;

public class InventoryMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int QuantityChange { get; set; } // Can be positive or negative

    public int PreviousQuantity { get; set; }

    public int NewQuantity { get; set; }

    public MovementType Type { get; set; }

    [Required]
    [StringLength(250)]
    public string Reason { get; set; } = string.Empty;

    public string? ReferenceId { get; set; } // e.g. OrderNumber or PO#

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

