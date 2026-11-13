using Ledgr.Api.Data;
using Ledgr.Api.Dtos;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Services;

public sealed class InventoryService(ApplicationDbContext database) : IInventoryService
{
    public async Task<InventoryMovementDto> AdjustAsync(StockAdjustmentDto dto, CancellationToken cancellationToken)
    {
        if (dto.Type is MovementType.OrderFulfill or MovementType.OrderCancel)
        {
            throw new LedgrException($"{dto.Type} movements are written by the order pipeline, not by hand.");
        }

        var product = await database.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId, cancellationToken)
            ?? throw new ProductNotFoundException(dto.ProductId);

        var movement = product.ApplyMovement(dto.QuantityChange, dto.Type, dto.Reason, dto.ReferenceId);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StockConflictException();
        }

        return InventoryMovementDto.From(movement, product);
    }

    public async Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(
        int? productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);

        var movements = database.InventoryMovements.AsNoTracking().AsQueryable();

        if (productId.HasValue)
        {
            movements = movements.Where(m => m.ProductId == productId.Value);
        }

        var totalCount = await movements.CountAsync(cancellationToken);
        var items = await movements
            .Include(m => m.Product)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(m => InventoryMovementDto.From(m, m.Product)).ToList();

        return new PagedResult<InventoryMovementDto>(dtos, normalizedPage, normalizedPageSize, totalCount);
    }
}
