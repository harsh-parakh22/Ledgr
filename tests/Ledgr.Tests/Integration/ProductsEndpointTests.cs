using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ledgr.Api.Contracts;

namespace Ledgr.Tests.Api;

public sealed class ProductsEndpointTests
{
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
}

