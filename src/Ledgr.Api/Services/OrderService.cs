using Ledgr.Api.Data;
using Ledgr.Api.Dtos;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Services;

public sealed class OrderService(ApplicationDbContext database) : IOrderService
{
    private const int MaxLinesPerOrder = 50;
    private const int MaxQuantityPerProduct = 10_000;

    public async Task<OrderCreationResult> CreateAsync(
        CreateOrderDto dto,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = NormalizeIdempotencyKey(idempotencyKey);

        if (key is not null)
        {
            var replay = await FindByIdempotencyKeyAsync(key, cancellationToken);
            if (replay is not null)
            {
                return new OrderCreationResult(replay, false);
            }
        }

        var lines = MergeLines(dto.Items);

        try
        {
            var order = await CreateCoreAsync(dto, lines, key, cancellationToken);
            return new OrderCreationResult(Map(order), true);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StockConflictException();
        }
        catch (DbUpdateException exception) when (key is not null && DbErrors.IsUniqueViolation(exception, "Orders.IdempotencyKey"))
        {
            // Another request with the same key won the race. Return its order instead of
            // creating a second one; the unique index is what makes this safe.
            database.ChangeTracker.Clear();

            var existing = await FindByIdempotencyKeyAsync(key, cancellationToken);
            if (existing is not null)
            {
                return new OrderCreationResult(existing, false);
            }

            throw new StockConflictException();
        }
    }

    public async Task<PagedResult<OrderDto>> GetAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);

        var totalCount = await database.Orders.CountAsync(cancellationToken);
        var orders = await database.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenByDescending(o => o.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderDto>(orders.Select(Map).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<OrderDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var order = await database.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new OrderNotFoundException(id);

        return Map(order);
    }

    public async Task<OrderDto> CancelAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var order = await database.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new OrderNotFoundException(id);

        if (order.Status == OrderStatus.Cancelled)
        {
            return Map(order);
        }

        foreach (var item in order.Items)
        {
            item.Product.ApplyMovement(
                item.Quantity,
                MovementType.OrderCancel,
                $"Order {order.OrderNumber} cancelled",
                order.OrderNumber);
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StockConflictException();
        }

        return Map(order);
    }

    private async Task<Order> CreateCoreAsync(
        CreateOrderDto dto,
        IReadOnlyList<OrderLine> lines,
        string? key,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var productIds = lines.Select(line => line.ProductId).ToList();
        var products = await database.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Reject the whole order before touching stock, so a bad line never costs a rollback.
        foreach (var productId in productIds)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                throw new ProductNotFoundException(productId);
            }

            if (!product.IsActive)
            {
                throw new InactiveProductException(product.Sku);
            }
        }

        var orderNumber = NewOrderNumber();
        var order = new Order
        {
            OrderNumber = orderNumber,
            IdempotencyKey = key ?? Guid.NewGuid().ToString("N"),
            CustomerName = dto.CustomerName.Trim(),
            CustomerEmail = Normalize(dto.CustomerEmail),
            Status = OrderStatus.Confirmed
        };

        decimal totalAmount = 0;

        foreach (var line in lines)
        {
            var product = products[line.ProductId];

            product.ApplyMovement(-line.Quantity, MovementType.OrderFulfill, $"Order {orderNumber}", orderNumber);
            order.Items.Add(new OrderItem
            {
                Product = product,
                Quantity = line.Quantity,
                UnitPrice = product.UnitPrice
            });

            totalAmount += product.UnitPrice * line.Quantity;
        }

        order.TotalAmount = totalAmount;
        database.Orders.Add(order);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return order;
    }

    private async Task<OrderDto?> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken)
    {
        var order = await database.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.IdempotencyKey == key, cancellationToken);

        return order is null ? null : Map(order);
    }

    private static List<OrderLine> MergeLines(IReadOnlyCollection<CreateOrderItemDto> items)
    {
        if (items.Count == 0)
        {
            throw new LedgrException("An order needs at least one line.");
        }

        if (items.Count > MaxLinesPerOrder)
        {
            throw new LedgrException($"An order cannot contain more than {MaxLinesPerOrder} lines.");
        }

        // Duplicate product lines are merged, but the original order is preserved so the
        // ledger records movements in the same sequence the request listed them.
        var merged = new List<OrderLine>();

        foreach (var item in items)
        {
            var index = merged.FindIndex(line => line.ProductId == item.ProductId);
            var quantity = (index < 0 ? 0 : merged[index].Quantity) + item.Quantity;

            if (quantity > MaxQuantityPerProduct)
            {
                throw new LedgrException($"A product cannot exceed {MaxQuantityPerProduct} units per order.");
            }

            if (index < 0)
            {
                merged.Add(new OrderLine(item.ProductId, quantity));
            }
            else
            {
                merged[index] = new OrderLine(item.ProductId, quantity);
            }
        }

        return merged;
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = value.Trim();
        if (key.Length > 64)
        {
            throw new LedgrException("Idempotency-Key must be 64 characters or fewer.");
        }

        return key;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NewOrderNumber()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }

    private static OrderDto Map(Order order) => new(
        order.Id,
        order.OrderNumber,
        order.CustomerName,
        order.CustomerEmail,
        order.Status.ToString(),
        order.TotalAmount,
        order.CreatedAtUtc,
        order.CancelledAtUtc,
        order.Items
            .Select(item => new OrderItemDto(
                item.Id,
                item.ProductId,
                item.Product.Name,
                item.Product.Sku,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal))
            .ToList());

    private readonly record struct OrderLine(int ProductId, int Quantity);
}
