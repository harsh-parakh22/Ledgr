using Microsoft.EntityFrameworkCore;
using Ledgr.Api.Data;
using Ledgr.Api.DTOs;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Models.Enums;

namespace Ledgr.Api.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;

    public ProductService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync(string? search = null, string? category = null, bool onlyLowStock = false)
    {
        var query = _context.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) || p.Sku.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (onlyLowStock)
        {
            query = query.Where(p => p.QuantityOnHand <= p.ReorderLevel);
        }

        var products = await query.OrderBy(p => p.Name).ToListAsync();

        return products.Select(MapToDto);
    }

    public async Task<ProductDto> GetByIdAsync(int id)
    {
        var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
            throw new ProductNotFoundException(id);

        return MapToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var skuUpper = dto.Sku.Trim().ToUpperInvariant();
        if (await _context.Products.AnyAsync(p => p.Sku == skuUpper))
            throw new DuplicateSkuException(skuUpper);

        var product = new Product
        {
            Sku = skuUpper,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Category = dto.Category.Trim(),
            UnitPrice = dto.UnitPrice,
            QuantityOnHand = dto.InitialQuantity,
            ReorderLevel = dto.ReorderLevel,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        if (dto.InitialQuantity > 0)
        {
            var initialLedger = new InventoryMovement
            {
                ProductId = product.Id,
                QuantityChange = dto.InitialQuantity,
                PreviousQuantity = 0,
                NewQuantity = dto.InitialQuantity,
                Type = MovementType.StockIn,
                Reason = "Initial Opening Stock on Product Creation",
                ReferenceId = $"PROD-INIT-{product.Id}",
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.InventoryMovements.Add(initialLedger);
            await _context.SaveChangesAsync();
        }

        return MapToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            throw new ProductNotFoundException(id);

        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim() ?? string.Empty;
        product.Category = dto.Category.Trim();
        product.UnitPrice = dto.UnitPrice;
        product.ReorderLevel = dto.ReorderLevel;
        product.IsActive = dto.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(product);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            throw new ProductNotFoundException(id);

        // Enterprise pattern: Soft delete to preserve referential integrity with orders and ledger
        product.IsActive = false;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private static ProductDto MapToDto(Product p) => new(
        p.Id,
        p.Sku,
        p.Name,
        p.Description,
        p.Category,
        p.UnitPrice,
        p.QuantityOnHand,
        p.ReorderLevel,
        p.IsActive,
        p.QuantityOnHand <= p.ReorderLevel,
        p.CreatedAtUtc
    );
}

