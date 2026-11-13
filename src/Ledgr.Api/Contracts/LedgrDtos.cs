using System.ComponentModel.DataAnnotations;
using Ledgr.Api.Domain;

namespace Ledgr.Api.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record ProductQuery(string? Search, string? Category, bool OnlyLowStock, int Page, int PageSize);

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

public sealed record CreateProductDto(
    [Required, StringLength(50, MinimumLength = 2)] string Sku,
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [StringLength(500)] string? Description,
    [Required, StringLength(80, MinimumLength = 2)] string Category,
    [Range(0.01, 1_000_000)] decimal UnitPrice,
    [Range(0, 1_000_000)] int InitialQuantity,
    [Range(0, 100_000)] int ReorderLevel);

public sealed record UpdateProductDto(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [StringLength(500)] string? Description,
    [Required, StringLength(80, MinimumLength = 2)] string Category,
    [Range(0.01, 1_000_000)] decimal UnitPrice,
    [Range(0, 100_000)] int ReorderLevel,
    bool IsActive);

public sealed record StockAdjustmentDto(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(-1_000_000, 1_000_000)] int QuantityChange,
    [EnumDataType(typeof(MovementType))] MovementType Type,
    [Required, StringLength(250, MinimumLength = 3)] string Reason,
    [StringLength(64)] string? ReferenceId);

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

public sealed record CreateOrderItemDto(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 10_000)] int Quantity);

public sealed record CreateOrderDto(
    [Required, StringLength(120, MinimumLength = 2)] string CustomerName,
    [EmailAddress, StringLength(150)] string? CustomerEmail,
    [Required, MinLength(1), MaxLength(50)] List<CreateOrderItemDto> Items);

public sealed record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerEmail,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyList<OrderItemDto> Items);

public sealed record DashboardKpisDto(
    int ActiveProducts,
    int LowStockCount,
    int OpenOrders,
    int CancelledOrders,
    decimal InventoryValue,
    IReadOnlyList<ProductDto> LowStockAlerts,
    IReadOnlyList<InventoryMovementDto> RecentMovements);
