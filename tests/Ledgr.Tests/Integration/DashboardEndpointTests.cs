using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ledgr.Api.Contracts;

namespace Ledgr.Tests.Api;

public sealed class DashboardEndpointTests
{
    [Fact]
    public async Task GetDashboard_ReturnsAggregates()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        await factory.AddProductAsync("SKU-KPI", stock: 4, price: 2.50m);

        var kpis = await client.GetFromJsonAsync<DashboardKpisDto>("/api/dashboard/kpis");

        kpis!.ActiveProducts.Should().Be(1);
        kpis.InventoryValue.Should().Be(10m);
        kpis.LowStockAlerts.Should().BeEmpty();
    }
}

