using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Ledgr.Api.Models.Enums;

namespace Ledgr.Api.Models;

[Index(nameof(OrderNumber), IsUnique = true)]
public class Order
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? CustomerEmail { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;

    [Precision(18, 2)]
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

