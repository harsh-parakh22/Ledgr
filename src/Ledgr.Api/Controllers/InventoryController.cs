using Ledgr.Api.Dtos;
using Ledgr.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ledgr.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpPost("adjust")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<InventoryMovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryMovementDto>> Adjust(
        [FromBody] StockAdjustmentDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await inventory.AdjustAsync(dto, cancellationToken));
    }

    [HttpGet("movements")]
    [ProducesResponseType<PagedResult<InventoryMovementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<InventoryMovementDto>>> GetMovements(
        [FromQuery] int? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return Ok(await inventory.GetMovementsAsync(productId, page, pageSize, cancellationToken));
    }
}
