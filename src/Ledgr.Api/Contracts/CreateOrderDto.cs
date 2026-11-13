using System.ComponentModel.DataAnnotations;

namespace Ledgr.Api.Contracts;

public sealed record CreateOrderDto(
    [Required, StringLength(120, MinimumLength = 2)] string CustomerName,
    [EmailAddress, StringLength(150)] string? CustomerEmail,
    [Required, MinLength(1), MaxLength(50)] List<CreateOrderItemDto> Items);

