using System.ComponentModel.DataAnnotations;
using Ledgr.Api.Models.Enums;

namespace Ledgr.Api.DTOs;

public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Description,
    string Category,
    decimal UnitPrice,
    int QuantityOnHand,
    int ReorderLevel,
    bool IsActive,
    bool IsLowStock,
    DateTime CreatedAtUtc
);

public record CreateProductDto(
    [Required, StringLength(50)] string Sku,
    [Required, StringLength(150)] string Name,
    string? Description,
    [Required, StringLength(80)] string Category,
    [Range(0.01, 1000000.00)] decimal UnitPrice,
    [Range(0, 100000)] int InitialQuantity,
    [Range(0, 10000)] int ReorderLevel
);

public record UpdateProductDto(
    [Required, StringLength(150)] string Name,
    string? Description,
    [Required, StringLength(80)] string Category,
    [Range(0.01, 1000000.00)] decimal UnitPrice,
    [Range(0, 10000)] int ReorderLevel,
    bool IsActive
);

public record StockAdjustmentDto(
    [Required] int ProductId,
    [Required] int QuantityChange, // + for restock, - for manual write-off
    [Required] MovementType Type,
    [Required, StringLength(250)] string Reason,
    string? ReferenceId
);

public record InventoryMovementDto(
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
    DateTime CreatedAtUtc
);

public record CreateOrderItemDto(
    [Required] int ProductId,
    [Range(1, 10000)] int Quantity
);

public record CreateOrderDto(
    [Required, StringLength(120)] string CustomerName,
    [EmailAddress] string? CustomerEmail,
    [Required, MinLength(1)] List<CreateOrderItemDto> Items
);

public record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal SubTotal
);

public record OrderDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerEmail,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    List<OrderItemDto> Items
);

public record DashboardKpisDto(
    int TotalProducts,
    int LowStockCount,
    int TotalOrders,
    decimal TotalInventoryValue,
    List<ProductDto> LowStockAlerts,
    List<InventoryMovementDto> RecentMovements
);

