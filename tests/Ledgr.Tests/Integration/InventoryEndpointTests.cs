using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ledgr.Api.Contracts;

namespace Ledgr.Tests.Api;

public sealed class InventoryEndpointTests
{
    [Fact]
    public async Task AdjustStock_ChangesStockAndAppearsInTheMovementLog()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-ADJ", stock: 5);

        var response = await client.PostAsJsonAsync("/api/inventory/adjust", new
        {
            productId,
            quantityChange = 3,
            type = 3,
            reason = "Cycle count",
            referenceId = "COUNT-1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var movement = await response.Content.ReadFromJsonAsync<InventoryMovementDto>();
        movement!.NewQuantity.Should().Be(8);

        var history = await client.GetFromJsonAsync<PagedResult<InventoryMovementDto>>("/api/inventory/movements?page=1&pageSize=5");
        history!.Items.Should().HaveCount(2);
        history.Items[0].Type.Should().Be("Adjustment");
        history.Items[0].ReferenceId.Should().Be("COUNT-1");
    }
}

