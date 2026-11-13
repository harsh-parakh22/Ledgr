using FluentAssertions;
using Ledgr.Api.Contracts;
using Ledgr.Api.Data;
using Ledgr.Api.Domain;
using Ledgr.Api.Services;
using Ledgr.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetAsync_AggregatesProductsOrdersAndValue()
    {
        using var database = new TestDatabase();
        await database.AddProductAsync("SKU-A", stock: 5, price: 10m);
        await database.AddProductAsync("SKU-B", stock: 1, price: 2m);

        await using var context = database.CreateContext();
        var orders = new OrderService(context);
        var dashboard = new DashboardService(context, new InventoryService(context));

        var product = await context.Products.AsNoTracking().SingleAsync(p => p.Sku == "SKU-A");
        await orders.CreateAsync(
            new CreateOrderDto("Acme", null, [new CreateOrderItemDto(product.Id, 1)]),
            null,
            CancellationToken.None);

        var kpis = await dashboard.GetAsync(CancellationToken.None);

        kpis.ActiveProducts.Should().Be(2);
        kpis.LowStockCount.Should().Be(1);
        kpis.OpenOrders.Should().Be(1);
        kpis.CancelledOrders.Should().Be(0);
        kpis.InventoryValue.Should().Be((10m * 4) + (2m * 1));
        kpis.LowStockAlerts.Should().ContainSingle().Which.Sku.Should().Be("SKU-B");
        kpis.RecentMovements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAsync_OnAnEmptyDatabase_ReturnsZeros()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var dashboard = new DashboardService(context, new InventoryService(context));

        var kpis = await dashboard.GetAsync(CancellationToken.None);

        kpis.ActiveProducts.Should().Be(0);
        kpis.InventoryValue.Should().Be(0m);
        kpis.LowStockAlerts.Should().BeEmpty();
        kpis.RecentMovements.Should().BeEmpty();
    }
}
