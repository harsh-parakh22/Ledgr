using Ledgr.Api.Contracts;

namespace Ledgr.Api.Services;

public interface IInventoryService
{
    Task<InventoryMovementDto> AdjustAsync(StockAdjustmentDto dto, CancellationToken cancellationToken);

    Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken);
}
