using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Range(1, 10000)]
    public int Quantity { get; set; }

    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    [Precision(18, 2)]
    public decimal SubTotal => Quantity * UnitPrice;
}

