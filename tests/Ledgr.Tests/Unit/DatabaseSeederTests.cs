using FluentAssertions;
using Ledgr.Api.Data;
using Ledgr.Api.Domain;
using Ledgr.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Tests;

public sealed class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesTheCatalogueWithOpeningMovements()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();

        await DatabaseSeeder.SeedAsync(context);

        var products = await context.Products.AsNoTracking().ToListAsync();
        var movements = await context.InventoryMovements.AsNoTracking().ToListAsync();

        products.Should().HaveCount(5);
        movements.Should().HaveCount(products.Count);
        movements.Should().OnlyContain(m => m.Type == MovementType.StockIn && m.PreviousQuantity == 0);
        products.Should().Contain(p => p.QuantityOnHand < p.ReorderLevel);
    }

    [Fact]
    public async Task SeedAsync_DoesNothingWhenTheCatalogueAlreadyHasProducts()
    {
        using var database = new TestDatabase();
        await database.AddProductAsync("SKU-EXISTING");

        await using var context = database.CreateContext();
        await DatabaseSeeder.SeedAsync(context);

        (await context.Products.CountAsync()).Should().Be(1);
    }
}
