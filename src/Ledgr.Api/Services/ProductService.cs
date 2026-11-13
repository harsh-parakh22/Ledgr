using Ledgr.Api.Contracts;
using Ledgr.Api.Data;
using Ledgr.Api.Domain;
using Ledgr.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Services;

public sealed class ProductService(ApplicationDbContext database) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var products = database.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLikePattern(query.Search)}%";
            products = products.Where(p =>
                EF.Functions.Like(p.Name, pattern, "\\") || EF.Functions.Like(p.Sku, pattern, "\\"));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            products = products.Where(p => p.Category == category);
        }

        if (query.OnlyLowStock)
        {
            products = products.Where(p => p.QuantityOnHand <= p.ReorderLevel);
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items.Select(ProductDto.From).ToList(), page, pageSize, totalCount);
    }

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        return ProductDto.From(product);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        return await database.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken)
    {
        var sku = dto.Sku.Trim().ToUpperInvariant();

        var product = new Product
        {
            Sku = sku,
            Name = dto.Name.Trim(),
            Description = Normalize(dto.Description),
            Category = dto.Category.Trim(),
            UnitPrice = dto.UnitPrice,
            ReorderLevel = dto.ReorderLevel
        };

        if (dto.InitialQuantity > 0)
        {
            product.ApplyMovement(dto.InitialQuantity, MovementType.StockIn, "Opening stock", null);
        }

        database.Products.Add(product);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception, "Products.Sku"))
        {
            throw new DuplicateSkuException(sku);
        }

        return ProductDto.From(product);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken)
    {
        var product = await database.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        product.Name = dto.Name.Trim();
        product.Description = Normalize(dto.Description);
        product.Category = dto.Category.Trim();
        product.UnitPrice = dto.UnitPrice;
        product.ReorderLevel = dto.ReorderLevel;
        product.IsActive = dto.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StockConflictException();
        }

        return ProductDto.From(product);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken)
    {
        var product = await database.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        if (!product.IsActive)
        {
            return;
        }

        product.IsActive = false;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StockConflictException();
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikePattern(string value) => value
        .Trim()
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
