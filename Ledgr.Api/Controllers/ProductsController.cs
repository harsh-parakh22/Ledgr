using Ledgr.Api.Dtos;
using Ledgr.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ledgr.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool onlyLowStock = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await products.GetAsync(
            new ProductQuery(search, category, onlyLowStock, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken)
    {
        return Ok(await products.GetCategoriesAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await products.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        var created = await products.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Update(
        int id,
        [FromBody] UpdateProductDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await products.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await products.DeactivateAsync(id, cancellationToken);

        return NoContent();
    }
}
