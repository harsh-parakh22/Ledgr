using Ledgr.Api.Dtos;

namespace Ledgr.Api.Services;

public interface IInventoryService
{
    Task<InventoryMovementDto> AdjustAsync(StockAdjustmentDto dto, CancellationToken cancellationToken);

    Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken);
}
