using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Ledgr.Api.Contracts;

namespace Ledgr.Tests.Api;

public sealed class ApiTests
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
    public async Task GetProducts_ReturnsAPagedEnvelope()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        await factory.AddProductAsync("SKU-ONE");
        await factory.AddProductAsync("SKU-TWO");

        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?page=1&pageSize=1");

        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle();
        page.TotalCount.Should().Be(2);
        page.TotalPages.Should().Be(2);
        page.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task DeactivateProduct_Returns204AndHidesItFromTheCatalogue()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-GONE");

        var delete = await client.DeleteAsync($"/api/products/{productId}");
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        page!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProduct_WhenMissing_Returns404ProblemDetails()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products/4242");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Health_ReturnsOkAndSetsSecurityHeaders()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateProduct_ReturnsTheUpdatedProduct()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        var productId = await factory.AddProductAsync("SKU-EDIT");

        var response = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            name = "Renamed product",
            description = "Updated",
            category = "Hardware",
            unitPrice = 42.50m,
            reorderLevel = 4,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product!.Name.Should().Be("Renamed product");
        product.UnitPrice.Should().Be(42.50m);
        product.Category.Should().Be("Hardware");
    }

    [Fact]
    public async Task GetCategories_ReturnsDistinctValuesInOrder()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();
        await factory.AddProductAsync("SKU-C1");

        var categories = await client.GetFromJsonAsync<List<string>>("/api/products/categories");

        categories.Should().NotBeNull();
        categories.Should().ContainSingle().Which.Should().Be("Tests");
    }

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

internal static class HttpRequestExtensions
{
    public static HttpRequestMessage WithHeader(this HttpRequestMessage request, string name, string value)
    {
        request.Headers.Add(name, value);
        return request;
    }
}
