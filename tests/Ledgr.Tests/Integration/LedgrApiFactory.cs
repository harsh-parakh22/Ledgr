using Ledgr.Api.Data;
using Ledgr.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ledgr.Tests.Api;

internal sealed class LedgrApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public LedgrApiFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task<int> AddProductAsync(string sku = "API-SKU", int stock = 10, decimal price = 5m)
    {
        await using var context = CreateDbContext();

        var product = new Product
        {
            Sku = sku,
            Name = $"Product {sku}",
            Category = "Tests",
            UnitPrice = price,
            ReorderLevel = 1
        };

        if (stock > 0)
        {
            product.ApplyMovement(stock, MovementType.StockIn, "Opening stock", null);
        }

        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:MigrateOnStartup"] = "false",
                ["Database:Seed"] = "false"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
