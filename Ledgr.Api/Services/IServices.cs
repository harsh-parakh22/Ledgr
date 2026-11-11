using Ledgr.Api.Dtos;

namespace Ledgr.Api.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken);

    Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken);

    Task DeactivateAsync(int id, CancellationToken cancellationToken);
}

public interface IInventoryService
{
    Task<InventoryMovementDto> AdjustAsync(StockAdjustmentDto dto, CancellationToken cancellationToken);

    Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<OrderCreationResult> CreateAsync(CreateOrderDto dto, string? idempotencyKey, CancellationToken cancellationToken);

    Task<PagedResult<OrderDto>> GetAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<OrderDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<OrderDto> CancelAsync(int id, CancellationToken cancellationToken);
}

public interface IDashboardService
{
    Task<DashboardKpisDto> GetAsync(CancellationToken cancellationToken);
}

/// <summary><paramref name="Created"/> is false when an idempotency key replayed an existing order.</summary>
public sealed record OrderCreationResult(OrderDto Order, bool Created);
