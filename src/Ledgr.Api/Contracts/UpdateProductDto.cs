using System.ComponentModel.DataAnnotations;

namespace Ledgr.Api.Contracts;

public sealed record UpdateProductDto(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [StringLength(500)] string? Description,
    [Required, StringLength(80, MinimumLength = 2)] string Category,
    [Range(0.01, 1_000_000)] decimal UnitPrice,
    [Range(0, 100_000)] int ReorderLevel,
    bool IsActive);

