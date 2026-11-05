using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ledgr.Api.Data;
using Ledgr.Api.DTOs;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Services;
using Xunit;

namespace Ledgr.Tests;

public class OrderServiceTests
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
    public async Task CreateOrder_WithSufficientStock_DeductsInventoryAndAppendsMovementLedger()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            Sku = "TEST-MOUSE-01",
            Name = "Ergo Mouse",
            Category = "Peripherals",
            UnitPrice = 50.00m,
            QuantityOnHand = 20,
            ReorderLevel = 5,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var orderService = new OrderService(context);

        var orderDto = new CreateOrderDto(
            CustomerName: "John Doe",
            CustomerEmail: "john@example.com",
            Items: new List<CreateOrderItemDto>
            {
                new(ProductId: product.Id, Quantity: 4)
            }
        );

        // Act
        var result = await orderService.CreateOrderAsync(orderDto);

        // Assert
        result.Should().NotBeNull();
        result.TotalAmount.Should().Be(200.00m);
        result.Items.Should().HaveCount(1);

        // Verify product stock updated
        var updatedProduct = await context.Products.FindAsync(product.Id);
        updatedProduct!.QuantityOnHand.Should().Be(16);

        // Verify ledger audit was created
        var movement = await context.InventoryMovements.FirstOrDefaultAsync(m => m.ProductId == product.Id);
        movement.Should().NotBeNull();
        movement!.QuantityChange.Should().Be(-4);
        movement.PreviousQuantity.Should().Be(20);
        movement.NewQuantity.Should().Be(16);
        movement.Reason.Should().Contain(result.OrderNumber);
    }

    [Fact]
    public async Task CreateOrder_WithInsufficientStock_ThrowsInsufficientStockExceptionAndRollsBack()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            Sku = "TEST-LAPTOP-01",
            Name = "Developer Laptop",
            Category = "Computers",
            UnitPrice = 1200.00m,
            QuantityOnHand = 2,
            ReorderLevel = 1,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var orderService = new OrderService(context);

        var orderDto = new CreateOrderDto(
            CustomerName: "Jane Smith",
            CustomerEmail: "jane@example.com",
            Items: new List<CreateOrderItemDto>
            {
                new(ProductId: product.Id, Quantity: 5) // Requesting 5 but only 2 available
            }
        );

        // Act
        var act = () => orderService.CreateOrderAsync(orderDto);

        // Assert
        await act.Should().ThrowAsync<InsufficientStockException>()
            .WithMessage("*TEST-LAPTOP-01*");

        // Verify stock was NOT altered
        var untouchedProduct = await context.Products.FindAsync(product.Id);
        untouchedProduct!.QuantityOnHand.Should().Be(2);

        // Verify no order created
        var orderCount = await context.Orders.CountAsync();
        orderCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateOrder_ForInactiveProduct_ThrowsInactiveProductException()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            Sku = "DISCONTINUED-SKU",
            Name = "Old Headset",
            Category = "Audio",
            UnitPrice = 30.00m,
            QuantityOnHand = 10,
            IsActive = false // Inactive
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var orderService = new OrderService(context);

        var orderDto = new CreateOrderDto(
            CustomerName: "Alice",
            CustomerEmail: "alice@example.com",
            Items: new List<CreateOrderItemDto>
            {
                new(ProductId: product.Id, Quantity: 1)
            }
        );

        // Act & Assert
        await Assert.ThrowsAsync<InactiveProductException>(() => orderService.CreateOrderAsync(orderDto));
    }
}
