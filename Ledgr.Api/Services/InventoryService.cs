using Microsoft.EntityFrameworkCore;
using Ledgr.Api.Data;
using Ledgr.Api.DTOs;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;

namespace Ledgr.Api.Services;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto dto)
    {
        if (dto.QuantityChange == 0)
            throw new LedgrException("Stock adjustment quantity change cannot be zero.", 400);

        // Transactional adjustment with ACID guarantee
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null)
                throw new ProductNotFoundException(dto.ProductId);

            var previousQty = product.QuantityOnHand;
            var newQty = previousQty + dto.QuantityChange;

            if (newQty < 0)
            {
                throw new InsufficientStockException(
                    product.Sku, 
                    Math.Abs(dto.QuantityChange), 
                    previousQty
                );
            }

            product.QuantityOnHand = newQty;
            product.UpdatedAtUtc = DateTime.UtcNow;

            var movement = new InventoryMovement
            {
                ProductId = product.Id,
                QuantityChange = dto.QuantityChange,
                PreviousQuantity = previousQty,
                NewQuantity = newQty,
                Type = dto.Type,
                Reason = dto.Reason.Trim(),
                ReferenceId = dto.ReferenceId?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.InventoryMovements.Add(movement);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new InventoryMovementDto(
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
                movement.CreatedAtUtc
            );
        });
    }

    public async Task<IEnumerable<InventoryMovementDto>> GetMovementsAsync(int? productId = null, int limit = 50)
    {
        var query = _context.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .AsQueryable();

        if (productId.HasValue)
        {
            query = query.Where(m => m.ProductId == productId.Value);
        }

        var list = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        return list.Select(m => new InventoryMovementDto(
            m.Id,
            m.ProductId,
            m.Product?.Name ?? "Unknown Product",
            m.Product?.Sku ?? "N/A",
            m.QuantityChange,
            m.PreviousQuantity,
            m.NewQuantity,
            m.Type.ToString(),
            m.Reason,
            m.ReferenceId,
            m.CreatedAtUtc
        ));
    }
}

