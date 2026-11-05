using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ledgr.Api.Data;
using Ledgr.Api.DTOs;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Models.Enums;
using Ledgr.Api.Services;
using Xunit;

namespace Ledgr.Tests;

public class InventoryServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task AdjustStock_PositiveRestock_IncreasesStockAndCreatesMovementRecord()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            Sku = "RESTOCK-TEST",
            Name = "USB-C Cable",
            Category = "Accessories",
            UnitPrice = 15.00m,
            QuantityOnHand = 10,
            ReorderLevel = 5,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        var adjustment = new StockAdjustmentDto(
            ProductId: product.Id,
            QuantityChange: 25,
            Type: MovementType.StockIn,
            Reason: "Shipment from supplier PO-9981",
            ReferenceId: "PO-9981"
        );

        // Act
        var result = await service.AdjustStockAsync(adjustment);

        // Assert
        result.Should().NotBeNull();
        result.QuantityChange.Should().Be(25);
        result.PreviousQuantity.Should().Be(10);
        result.NewQuantity.Should().Be(35);

        var updated = await context.Products.FindAsync(product.Id);
        updated!.QuantityOnHand.Should().Be(35);
    }

    [Fact]
    public async Task AdjustStock_NegativeWriteOffExceedingCurrentStock_ThrowsInsufficientStockException()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            Sku = "WRITEOFF-TEST",
            Name = "Fragile Glass Screen",
            Category = "Displays",
            UnitPrice = 80.00m,
            QuantityOnHand = 3,
            ReorderLevel = 2,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        var adjustment = new StockAdjustmentDto(
            ProductId: product.Id,
            QuantityChange: -5, // Writing off 5 when only 3 are on hand
            Type: MovementType.WriteOff,
            Reason: "Warehouse flood damage",
            ReferenceId: "CLAIM-101"
        );

        // Act
        var act = () => service.AdjustStockAsync(adjustment);

        // Assert
        await act.Should().ThrowAsync<InsufficientStockException>();
    }
}
