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
