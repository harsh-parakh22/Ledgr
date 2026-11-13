using System.ComponentModel.DataAnnotations;

namespace Ledgr.Api.Contracts;

public sealed record CreateOrderItemDto(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 10_000)] int Quantity);

