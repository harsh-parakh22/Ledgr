using Ledgr.Api.Contracts;

namespace Ledgr.Api.Services;

public interface IOrderService
{
    Task<OrderCreationResult> CreateAsync(CreateOrderDto dto, string? idempotencyKey, CancellationToken cancellationToken);

    Task<PagedResult<OrderDto>> GetAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<OrderDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<OrderDto> CancelAsync(int id, CancellationToken cancellationToken);
}

/// <summary><paramref name="Created"/> is false when an idempotency key replayed an existing order.</summary>
public sealed record OrderCreationResult(OrderDto Order, bool Created);
