using Ledgr.Api.DTOs;
using Ledgr.Api.Models;

namespace Ledgr.Api.Services;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllAsync(string? search = null, string? category = null, bool onlyLowStock = false);
    Task<ProductDto> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto);
    Task<bool> DeleteAsync(int id); // Soft deactivates
}

public interface IInventoryService
{
    Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto dto);
    Task<IEnumerable<InventoryMovementDto>> GetMovementsAsync(int? productId = null, int limit = 50);
}

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
    Task<IEnumerable<OrderDto>> GetOrdersAsync(int limit = 50);
    Task<OrderDto> GetOrderByIdAsync(int id);
}

public interface IDashboardService
{
    Task<DashboardKpisDto> GetKpisAsync();
}

