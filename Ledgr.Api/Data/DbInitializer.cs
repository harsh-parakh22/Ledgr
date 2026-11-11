using Ledgr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext database, CancellationToken cancellationToken = default)
    {
        if (await database.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var products = new List<Product>
        {
            Create("SKU-LOGI-MX3S", "Logitech MX Master 3S", "Peripherals", 99.99m, 45, 10,
                "Wireless mouse, 8K DPI, quiet switches."),
            Create("SKU-KCH-K2V2", "Keychron K2 V2", "Peripherals", 79.50m, 8, 10,
                "75% mechanical keyboard, Gateron Brown switches."),
            Create("SKU-DELL-U2723", "Dell UltraSharp U2723QE", "Displays", 579.00m, 14, 5,
                "27-inch 4K monitor with USB-C power delivery."),
            Create("SKU-ANKR-737", "Anker 737 Power Bank", "Accessories", 149.99m, 3, 8,
                "24,000 mAh, 140 W, three ports."),
            Create("SKU-SNY-WH1000XM5", "Sony WH-1000XM5", "Audio", 348.00m, 22, 6,
                "Over-ear noise cancelling headphones.")
        };

        database.Products.AddRange(products);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static Product Create(
        string sku,
        string name,
        string category,
        decimal unitPrice,
        int openingStock,
        int reorderLevel,
        string description)
    {
        var product = new Product
        {
            Sku = sku,
            Name = name,
            Category = category,
            Description = description,
            UnitPrice = unitPrice,
            ReorderLevel = reorderLevel
        };

        product.ApplyMovement(openingStock, MovementType.StockIn, "Opening stock", "SEED");

        return product;
    }
}
