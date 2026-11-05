using Microsoft.EntityFrameworkCore;
using Ledgr.Api.Data;
using Ledgr.Api.DTOs;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Models.Enums;

namespace Ledgr.Api.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;

    public OrderService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new LedgrException("An order must contain at least one item.", 400);

        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            // Explicit ACID Transaction to guarantee atomicity and avoid race conditions/overselling
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var requestedProductIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => requestedProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var order = new Order
            {
                OrderNumber = orderNumber,
                CustomerName = dto.CustomerName.Trim(),
                CustomerEmail = dto.CustomerEmail?.Trim(),
                Status = OrderStatus.Confirmed,
                CreatedAtUtc = DateTime.UtcNow
            };

            decimal totalAmount = 0;

            foreach (var itemDto in dto.Items)
            {
                if (!products.TryGetValue(itemDto.ProductId, out var product))
                {
                    throw new ProductNotFoundException(itemDto.ProductId);
                }

                if (!product.IsActive)
                {
                    throw new InactiveProductException(product.Sku);
                }

                // Concurrency & Stock Check
                if (product.QuantityOnHand < itemDto.Quantity)
                {
                    throw new InsufficientStockException(product.Sku, itemDto.Quantity, product.QuantityOnHand);
                }

                var previousStock = product.QuantityOnHand;
                var newStock = previousStock - itemDto.Quantity;

                // 1. Decrement Stock
                product.QuantityOnHand = newStock;
                product.UpdatedAtUtc = DateTime.UtcNow;

                // 2. Append Double-entry Inventory Ledger Movement
                var movement = new InventoryMovement
                {
                    ProductId = product.Id,
                    QuantityChange = -itemDto.Quantity,
                    PreviousQuantity = previousStock,
                    NewQuantity = newStock,
                    Type = MovementType.OrderFulfill,
                    Reason = $"Order Fulfillment {orderNumber}",
                    ReferenceId = orderNumber,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _context.InventoryMovements.Add(movement);

                // 3. Attach Order Line Item
                var orderItem = new OrderItem
                {
                    Order = order,
                    ProductId = product.Id,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.UnitPrice
                };
                order.Items.Add(orderItem);

                totalAmount += (product.UnitPrice * itemDto.Quantity);
            }

            order.TotalAmount = totalAmount;
            _context.Orders.Add(order);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return MapToDto(order);
        });
    }

    public async Task<IEnumerable<OrderDto>> GetOrdersAsync(int limit = 50)
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        return orders.Select(MapToDto);
    }

    public async Task<OrderDto> GetOrderByIdAsync(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            throw new LedgrException($"Order with ID '{id}' was not found.", 404);

        return MapToDto(order);
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id,
        o.OrderNumber,
        o.CustomerName,
        o.CustomerEmail,
        o.Status.ToString(),
        o.TotalAmount,
        o.CreatedAtUtc,
        o.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.Product?.Name ?? "Product",
            i.Product?.Sku ?? "N/A",
            i.Quantity,
            i.UnitPrice,
            i.SubTotal
        )).ToList()
    );
}

