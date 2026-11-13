using System.ComponentModel.DataAnnotations;
using Ledgr.Api.Domain;

namespace Ledgr.Api.Contracts;

public sealed record StockAdjustmentDto(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(-1_000_000, 1_000_000)] int QuantityChange,
    [EnumDataType(typeof(MovementType))] MovementType Type,
    [Required, StringLength(250, MinimumLength = 3)] string Reason,
    [StringLength(64)] string? ReferenceId);

