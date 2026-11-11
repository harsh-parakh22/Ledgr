using FluentAssertions;
using Ledgr.Api.Dtos;
using Ledgr.Api.Exceptions;
using Ledgr.Api.Models;
using Ledgr.Api.Services;
using Ledgr.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Tests;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task CreateAsync_WithOpeningStock_RecordsTheMovement()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var product = await service.CreateAsync(
            new CreateProductDto("sku-100", "Desk lamp", null, "Lighting", 24.50m, 6, 2),
            CancellationToken.None);

        product.QuantityOnHand.Should().Be(6);
        product.Sku.Should().Be("SKU-100");

        var movements = await context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.ProductId == product.Id)
            .ToListAsync();

        movements.Should().ContainSingle();
        movements[0].Type.Should().Be(MovementType.StockIn);
        movements[0].PreviousQuantity.Should().Be(0);
        movements[0].NewQuantity.Should().Be(6);
    }

    [Fact]
    public async Task CreateAsync_WithZeroOpeningStock_WritesNoMovement()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var product = await service.CreateAsync(
            new CreateProductDto("SKU-101", "Cable", null, "Cables", 4.99m, 0, 1),
            CancellationToken.None);

        (await context.InventoryMovements.CountAsync(m => m.ProductId == product.Id)).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSku_Throws409()
    {
        using var database = new TestDatabase();
        await database.AddProductAsync("SKU-DUP");
        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var act = () => service.CreateAsync(
            new CreateProductDto("sku-dup", "Another", null, "Tests", 1m, 0, 0),
            CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateSkuException>();
    }

    [Fact]
    public async Task UpdateAsync_ChangesEditableFields()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync();
        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var updated = await service.UpdateAsync(
            product.Id,
            new UpdateProductDto("Renamed", "New description", "Hardware", 19.99m, 7, true),
            CancellationToken.None);

        updated.Name.Should().Be("Renamed");
        updated.Category.Should().Be("Hardware");
        updated.UnitPrice.Should().Be(19.99m);
        updated.ReorderLevel.Should().Be(7);
    }

    [Fact]
    public async Task DeactivateAsync_KeepsTheRowAndHidesItFromTheCatalogue()
    {
        using var database = new TestDatabase();
        var product = await database.AddProductAsync();
        await using var context = database.CreateContext();
        var service = new ProductService(context);

        await service.DeactivateAsync(product.Id, CancellationToken.None);

        var stored = await context.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        stored.IsActive.Should().BeFalse();

        var catalogue = await service.GetAsync(new ProductQuery(null, null, false, 1, 25), CancellationToken.None);
        catalogue.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_FiltersBySearchCategoryAndLowStock()
    {
        using var database = new TestDatabase();
        await database.AddProductAsync("SKU-MOUSE", stock: 20, category: "Peripherals");
        await database.AddProductAsync("SKU-KEYBOARD", stock: 1, category: "Peripherals");
        await database.AddProductAsync("SKU-MONITOR", stock: 1, category: "Displays");

        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var bySearch = await service.GetAsync(new ProductQuery("mouse", null, false, 1, 25), CancellationToken.None);
        bySearch.Items.Should().ContainSingle().Which.Sku.Should().Be("SKU-MOUSE");

        var byCategory = await service.GetAsync(new ProductQuery(null, "Displays", false, 1, 25), CancellationToken.None);
        byCategory.Items.Should().ContainSingle().Which.Sku.Should().Be("SKU-MONITOR");

        var lowStock = await service.GetAsync(new ProductQuery(null, null, true, 1, 25), CancellationToken.None);
        lowStock.Items.Select(p => p.Sku).Should().BeEquivalentTo("SKU-KEYBOARD", "SKU-MONITOR");
    }

    [Fact]
    public async Task GetAsync_PaginatesAndCapsThePageSize()
    {
        using var database = new TestDatabase();
        for (var i = 0; i < 3; i++)
        {
            await database.AddProductAsync($"SKU-{i:D3}");
        }

        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var firstPage = await service.GetAsync(new ProductQuery(null, null, false, 1, 2), CancellationToken.None);
        firstPage.Items.Should().HaveCount(2);
        firstPage.TotalCount.Should().Be(3);
        firstPage.TotalPages.Should().Be(2);

        var oversized = await service.GetAsync(new ProductQuery(null, null, false, 1, 5_000), CancellationToken.None);
        oversized.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetAsync_SearchTreatsWildcardsAsLiteralText()
    {
        using var database = new TestDatabase();
        await database.AddProductAsync("SKU-PLAIN", stock: 5);

        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var result = await service.GetAsync(new ProductQuery("%", null, false, 1, 25), CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_Throws404()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new ProductService(context);

        var act = () => service.GetByIdAsync(999, CancellationToken.None);

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }
}
