namespace Ledgr.Api.Contracts;

public sealed record DashboardKpisDto(
    int ActiveProducts,
    int LowStockCount,
    int OpenOrders,
    int CancelledOrders,
    decimal InventoryValue,
    IReadOnlyList<ProductDto> LowStockAlerts,
    IReadOnlyList<InventoryMovementDto> RecentMovements);

