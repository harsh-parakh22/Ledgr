using Ledgr.Api.Contracts;
using Ledgr.Api.Data;
using Ledgr.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Services;

public sealed class DashboardService(ApplicationDbContext database, IInventoryService inventory) : IDashboardService
{
    private const int LowStockAlertLimit = 20;
    private const int RecentMovementLimit = 8;

    public async Task<DashboardKpisDto> GetAsync(CancellationToken cancellationToken)
    {
        var activeProducts = database.Products.AsNoTracking().Where(p => p.IsActive);

        var activeProductCount = await activeProducts.CountAsync(cancellationToken);
        var lowStockCount = await activeProducts.CountAsync(p => p.QuantityOnHand <= p.ReorderLevel, cancellationToken);
        var openOrders = await database.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed, cancellationToken);
        var cancelledOrders = await database.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled, cancellationToken);
        var inventoryValue = await InventoryValueAsync(activeProducts, cancellationToken);

        var lowStockProducts = await activeProducts
            .Where(p => p.QuantityOnHand <= p.ReorderLevel)
            .OrderBy(p => p.QuantityOnHand - p.ReorderLevel)
            .ThenBy(p => p.Name)
            .Take(LowStockAlertLimit)
            .ToListAsync(cancellationToken);

        var recentMovements = await inventory.GetMovementsAsync(null, 1, RecentMovementLimit, cancellationToken);

        return new DashboardKpisDto(
            activeProductCount,
            lowStockCount,
            openOrders,
            cancelledOrders,
            inventoryValue,
            lowStockProducts.Select(ProductDto.From).ToList(),
            recentMovements.Items);
    }

    private static async Task<decimal> InventoryValueAsync(IQueryable<Product> activeProducts, CancellationToken cancellationToken)
    {
        // SQLite stores decimal as text, so summing in SQL would lose exactness. Reading the
        // two columns and summing in memory keeps the figure exact at this application's size.
        var lines = await activeProducts
            .Select(p => new { p.UnitPrice, p.QuantityOnHand })
            .ToListAsync(cancellationToken);

        return lines.Sum(line => line.UnitPrice * line.QuantityOnHand);
    }
}
