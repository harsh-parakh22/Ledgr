using Microsoft.EntityFrameworkCore;
using Ledgr.Api.Models;
using Ledgr.Api.Models.Enums;

namespace Ledgr.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // For SqlServer migrations or In-Memory database ensuring created
        await context.Database.EnsureCreatedAsync();

        if (await context.Products.AnyAsync())
        {
            return; // DB already seeded
        }

        var products = new List<Product>
        {
            new()
            {
                Sku = "SKU-LOGI-MX3S",
                Name = "Logitech MX Master 3S Wireless Mouse",
                Category = "Peripherals",
                Description = "Ergonomic quiet click 8K DPI wireless mouse",
                UnitPrice = 99.99m,
                QuantityOnHand = 45,
                ReorderLevel = 10,
                IsActive = true
            },
            new()
            {
                Sku = "SKU-KCH-K2V2",
                Name = "Keychron K2 V2 Mechanical Keyboard",
                Category = "Peripherals",
                Description = "75% layout wireless mechanical keyboard with Gateron Brown switches",
                UnitPrice = 79.50m,
                QuantityOnHand = 8, // Low stock! Below reorder level of 10
                ReorderLevel = 10,
                IsActive = true
            },
            new()
            {
                Sku = "SKU-DELL-U2723",
                Name = "Dell UltraSharp 27 4K USB-C Hub Monitor",
                Category = "Displays",
                Description = "27-inch 4K IPS Black monitor with 90W power delivery",
                UnitPrice = 579.00m,
                QuantityOnHand = 14,
                ReorderLevel = 5,
                IsActive = true
            },
            new()
            {
                Sku = "SKU-ANKR-737",
                Name = "Anker 737 Power Bank (PowerCore 24K)",
                Category = "Accessories",
                Description = "24,000mAh 140W 3-port portable charger with smart digital display",
                UnitPrice = 149.99m,
                QuantityOnHand = 3, // Critical low stock!
                ReorderLevel = 8,
                IsActive = true
            },
            new()
            {
                Sku = "SKU-SNY-WH1000XM5",
                Name = "Sony WH-1000XM5 Noise Canceling Headphones",
                Category = "Audio",
                Description = "Industry leading noise canceling wireless headphones",
                UnitPrice = 348.00m,
                QuantityOnHand = 22,
                ReorderLevel = 6,
                IsActive = true
            }
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();

        // Add initial ledger entries for the initial stocks
        var movements = products.Select(p => new InventoryMovement
        {
            ProductId = p.Id,
            QuantityChange = p.QuantityOnHand,
            PreviousQuantity = 0,
            NewQuantity = p.QuantityOnHand,
            Type = MovementType.StockIn,
            Reason = "Initial Opening Stock Inventory",
            ReferenceId = "INIT-STOCK-2026",
            CreatedAtUtc = DateTime.UtcNow
        }).ToList();

        await context.InventoryMovements.AddRangeAsync(movements);
        await context.SaveChangesAsync();
    }
}

