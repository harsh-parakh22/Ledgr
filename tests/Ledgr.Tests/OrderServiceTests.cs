using FluentAssertions;
using Ledgr.Api.Dtos;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Services;
using Ledgr.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_WithStock_DeductsStockAndWritesLedger()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 20, price: 50m);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var result = await service.CreateAsync(
            new CreateOrderDto("Acme", "buyer@acme.test", [new CreateOrderItemDto(product.Id, 4)]),
            null,
            CancellationToken.None);

        result.Created.Should().BeTrue();
        result.Order.TotalAmount.Should().Be(200m);
        (await database.StockAsync(product.Id)).Should().Be(16);

        var movement = await context.InventoryMovements.AsNoTracking().SingleAsync(m => m.Type == MovementType.OrderFulfill);
        movement.QuantityChange.Should().Be(-4);
        movement.PreviousQuantity.Should().Be(20);
        movement.NewQuantity.Should().Be(16);
        movement.ReferenceId.Should().Be(result.Order.OrderNumber);
    }

    [Fact]
    public async Task CreateAsync_ReturnsLineItemsWithProductDetails()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync("SKU-REAL", stock: 5);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var result = await service.CreateAsync(
            new CreateOrderDto("Acme", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);

        var line = result.Order.Items.Should().ContainSingle().Subject;
        line.ProductName.Should().Be("Product SKU-REAL");
        line.Sku.Should().Be("SKU-REAL");
        line.LineTotal.Should().Be(10m);
    }

    [Fact]
    public async Task CreateAsync_WhenASecondLineHasNoStock_RollsBackTheFirst()
    {
        using var database = new TestDatabase();
        var inStock = await database.AddProductAsync("SKU-OK", stock: 10);
        var empty = await database.AddProductAsync("SKU-EMPTY", stock: 0);

        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var act = () => service.CreateAsync(
            new CreateOrderDto("Acme", null,
            [
                new CreateOrderItemDto(inStock.Id, 2),
                new CreateOrderItemDto(empty.Id, 1)
            ]),
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();

        (await database.StockAsync(inStock.Id)).Should().Be(10);

        await using var verification = database.CreateContext();
        (await verification.Orders.CountAsync()).Should().Be(0);
        (await verification.InventoryMovements.CountAsync(m => m.ProductId == inStock.Id)).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ForInactiveProduct_IsRejected()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(active: false);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var act = () => service.CreateAsync(
            new CreateOrderDto("Acme", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InactiveProductException>();
    }

    [Fact]
    public async Task CreateAsync_MergesDuplicateProductLines()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10, price: 3m);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var result = await service.CreateAsync(
            new CreateOrderDto("Acme", null,
            [
                new CreateOrderItemDto(product.Id, 2),
                new CreateOrderItemDto(product.Id, 3)
            ]),
            null,
            CancellationToken.None);

        result.Order.Items.Should().ContainSingle().Which.Quantity.Should().Be(5);
        result.Order.TotalAmount.Should().Be(15m);
        (await database.StockAsync(product.Id)).Should().Be(5);
    }

    [Fact]
    public async Task CreateAsync_WithSameIdempotencyKey_ReturnsTheOriginalOrder()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var dto = new CreateOrderDto("Acme", null, [new CreateOrderItemDto(product.Id, 2)]);

        var first = await service.CreateAsync(dto, "key-1", CancellationToken.None);
        var second = await service.CreateAsync(dto, "key-1", CancellationToken.None);

        first.Created.Should().BeTrue();
        second.Created.Should().BeFalse();
        second.Order.Id.Should().Be(first.Order.Id);
        (await database.StockAsync(product.Id)).Should().Be(8);

        await using var verification = database.CreateContext();
        (await verification.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WhenStockChangedUnderneath_RaisesConflictInsteadOfOverselling()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 5);

        // This context reads the product first, then keeps that stale snapshot.
        await using var staleContext = database.CreateContext();
        await staleContext.Products.SingleAsync(p => p.Id == product.Id);

        await using var freshContext = database.CreateContext();
        var first = await new OrderService(freshContext).CreateAsync(
            new CreateOrderDto("First", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);
        first.Created.Should().BeTrue();

        var act = () => new OrderService(staleContext).CreateAsync(
            new CreateOrderDto("Second", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<StockConflictException>();
        (await database.StockAsync(product.Id)).Should().Be(4);
    }

    [Fact]
    public async Task CancelAsync_RestoresStockAndMarksTheOrderCancelled()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var created = await service.CreateAsync(
            new CreateOrderDto("Acme", null, [new CreateOrderItemDto(product.Id, 3)]),
            null,
            CancellationToken.None);

        var cancelled = await service.CancelAsync(created.Order.Id, CancellationToken.None);

        cancelled.Status.Should().Be(nameof(OrderStatus.Cancelled));
        cancelled.CancelledAtUtc.Should().NotBeNull();
        (await database.StockAsync(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task CancelAsync_WhenCalledTwice_DoesNotRestoreStockTwice()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var created = await service.CreateAsync(
            new CreateOrderDto("Acme", null, [new CreateOrderItemDto(product.Id, 3)]),
            null,
            CancellationToken.None);

        await service.CancelAsync(created.Order.Id, CancellationToken.None);
        await service.CancelAsync(created.Order.Id, CancellationToken.None);

        (await database.StockAsync(product.Id)).Should().Be(10);

        await using var verification = database.CreateContext();
        (await verification.InventoryMovements.CountAsync(m => m.Type == MovementType.OrderCancel)).Should().Be(1);
    }

    [Fact]
    public async Task CancelAsync_WhenOrderIsMissing_Throws404()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var act = () => service.CancelAsync(404, CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public async Task GetAsync_ReturnsNewestOrderFirst()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10);
        await using var context = database.CreateContext();
        var service = new OrderService(context);

        var first = await service.CreateAsync(
            new CreateOrderDto("First", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);
        var second = await service.CreateAsync(
            new CreateOrderDto("Second", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);

        var page = await service.GetAsync(1, 10, CancellationToken.None);

        page.Items[0].Id.Should().Be(second.Order.Id);
        page.Items[1].Id.Should().Be(first.Order.Id);
    }
}
