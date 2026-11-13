using FluentAssertions;
using Ledgr.Api.Dtos;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Services;
using Ledgr.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Tests;

public sealed class InventoryServiceTests
{
    [Fact]
    public async Task AdjustAsync_StockIn_IncreasesStockAndRecordsMovement()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10);
        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        var movement = await service.AdjustAsync(
            new StockAdjustmentDto(product.Id, 25, MovementType.StockIn, "Supplier delivery", "PO-1"),
            CancellationToken.None);

        movement.PreviousQuantity.Should().Be(10);
        movement.NewQuantity.Should().Be(35);
        (await database.StockAsync(product.Id)).Should().Be(35);
    }

    [Fact]
    public async Task AdjustAsync_WriteOffBeyondStock_ThrowsAndLeavesStockAlone()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 3);
        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        var act = () => service.AdjustAsync(
            new StockAdjustmentDto(product.Id, -5, MovementType.WriteOff, "Water damage", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();
        (await database.StockAsync(product.Id)).Should().Be(3);
    }

    [Fact]
    public async Task AdjustAsync_ZeroChange_IsRejected()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync();
        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        var act = () => service.AdjustAsync(
            new StockAdjustmentDto(product.Id, 0, MovementType.Adjustment, "Recount", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<LedgrException>();
    }

    [Fact]
    public async Task AdjustAsync_WithOrderPipelineType_IsRejected()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync();
        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        var act = () => service.AdjustAsync(
            new StockAdjustmentDto(product.Id, -1, MovementType.OrderFulfill, "Manual pick", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<LedgrException>()
            .WithMessage("*order pipeline*");
    }

    [Fact]
    public async Task AdjustAsync_WriteOffWithPositiveQuantity_IsRejected()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 5);
        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        var act = () => service.AdjustAsync(
            new StockAdjustmentDto(product.Id, 4, MovementType.WriteOff, "Wrong direction", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<LedgrException>()
            .WithMessage("*WriteOff*positive*");
    }

    [Fact]
    public async Task GetMovementsAsync_ReturnsNewestFirst()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync(stock: 10);
        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        await service.AdjustAsync(new StockAdjustmentDto(product.Id, 1, MovementType.Adjustment, "First", null), CancellationToken.None);
        await service.AdjustAsync(new StockAdjustmentDto(product.Id, 2, MovementType.Adjustment, "Second", null), CancellationToken.None);

        var movements = await service.GetMovementsAsync(product.Id, 1, 10, CancellationToken.None);

        movements.TotalCount.Should().Be(3);
        movements.Items[0].Reason.Should().Be("Second");
        movements.Items[1].Reason.Should().Be("First");
    }

    [Fact]
    public async Task GetMovementsAsync_CanBeScopedToAProduct()
    {
        using var database = new TestDatabase();
        var first = await database.AddProductAsync("SKU-A", stock: 5);
        var second = await database.AddProductAsync("SKU-B", stock: 5);

        await using var context = database.CreateContext();
        var service = new InventoryService(context);

        await service.AdjustAsync(new StockAdjustmentDto(second.Id, 1, MovementType.Adjustment, "Only B", null), CancellationToken.None);

        var movements = await service.GetMovementsAsync(first.Id, 1, 10, CancellationToken.None);

        movements.Items.Should().ContainSingle();
        movements.Items[0].Sku.Should().Be("SKU-A");
    }
}
