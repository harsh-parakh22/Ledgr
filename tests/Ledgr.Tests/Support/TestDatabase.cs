using Ledgr.Api.Data;
using Ledgr.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Tests.Support;

/// <summary>
/// A real SQLite database per test. In-memory SQLite keeps transactions and constraints,
/// which the EF in-memory provider does not, so rollback behaviour is actually exercised.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task<Product> AddProductAsync(
        string sku = "TEST-SKU-1",
        int stock = 10,
        decimal price = 10m,
        bool active = true,
        string category = "Tests")
    {
        await using var context = CreateContext();

        var product = new Product
        {
            Sku = sku,
            Name = $"Product {sku}",
            Category = category,
            Description = null,
            UnitPrice = price,
            ReorderLevel = 2,
            IsActive = active
        };

        if (stock > 0)
        {
            product.ApplyMovement(stock, MovementType.StockIn, "Opening stock", null);
        }

        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product;
    }

    public async Task<int> StockAsync(int productId)
    {
        await using var context = CreateContext();

        return await context.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.QuantityOnHand)
            .SingleAsync();
    }

    public void Dispose() => _connection.Dispose();
}
