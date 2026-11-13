using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Ledgr.Api.Contracts;
using Ledgr.Tests.Support;

namespace Ledgr.Tests.Api;

public sealed class OrdersEndpointTests
{
    [Fact]
    public async Task PostOrder_Returns201WithLineItemDetails()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-API", stock: 5, price: 12.50m);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Acme",
            items = new[] { new { productId, quantity = 2 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        order!.Items.Should().ContainSingle();
        order.Items[0].Sku.Should().Be("SKU-API");
        order.Items[0].ProductName.Should().Be("Product SKU-API");
        order.TotalAmount.Should().Be(25m);
    }

    [Fact]
    public async Task PostOrder_WhenStockIsShort_Returns409ProblemDetails()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-SHORT", stock: 1);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Acme",
            items = new[] { new { productId, quantity = 3 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be("Insufficient stock");
        problem.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostOrder_WithInvalidBody_Returns400WithFieldErrors()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new { customerName = "", items = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fieldNames = problem.RootElement.GetProperty("errors").EnumerateObject().Select(field => field.Name);
        fieldNames.Should().Contain(name => name.Equals("customerName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostOrder_WithIdempotencyKey_ReplaysTheOriginalOrder()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-IDEM", stock: 4);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { customerName = "Acme", items = new[] { new { productId, quantity = 1 } } })
        };
        request.Headers.Add("Idempotency-Key", "abc-123");

        var first = await client.SendAsync(request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var replay = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { customerName = "Acme", items = new[] { new { productId, quantity = 1 } } })
        }.WithHeader("Idempotency-Key", "abc-123"));

        replay.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstOrder = await first.Content.ReadFromJsonAsync<OrderDto>();
        var replayOrder = await replay.Content.ReadFromJsonAsync<OrderDto>();
        replayOrder!.Id.Should().Be(firstOrder!.Id);

        await using var context = factory.CreateDbContext();
        context.Orders.Count().Should().Be(1);
        context.Products.Single().QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public async Task CancelOrder_ReturnsUnitsToStock()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-CANCEL", stock: 6);

        var created = await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Acme",
            items = new[] { new { productId, quantity = 2 } }
        });
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();

        var cancelled = await client.PostAsync($"/api/orders/{order!.Id}/cancel", null);

        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await client.GetFromJsonAsync<ProductDto>($"/api/products/{productId}");
        after!.QuantityOnHand.Should().Be(6);
    }

    [Fact]
    public async Task GetOrderById_ReturnsTheOrderWithItsLines()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-READ", stock: 3);

        var created = await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Acme",
            items = new[] { new { productId, quantity = 1 } }
        });
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();

        var fetched = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order!.Id}");

        fetched!.OrderNumber.Should().Be(order.OrderNumber);
        fetched.Items.Should().ContainSingle().Which.Sku.Should().Be("SKU-READ");
    }

    [Fact]
    public async Task GetOrders_ReturnsAPagedEnvelope()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-LIST", stock: 5);

        await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Acme",
            items = new[] { new { productId, quantity = 1 } }
        });

        var page = await client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?page=1&pageSize=5");

        page!.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
    }
}

