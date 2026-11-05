using Microsoft.EntityFrameworkCore;
using Ledgr.Api.Data;
using Ledgr.Api.DTOs;

namespace Ledgr.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;

    public DashboardService(ApplicationDbContext context, IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    public async Task<DashboardKpisDto> GetKpisAsync()
    {
        var activeProducts = await _context.Products.AsNoTracking().Where(p => p.IsActive).ToListAsync();

        var totalProducts = activeProducts.Count;
        var lowStockProducts = activeProducts.Where(p => p.QuantityOnHand <= p.ReorderLevel).ToList();
        var lowStockCount = lowStockProducts.Count;
        var totalOrders = await _context.Orders.CountAsync();
        var totalInventoryValue = activeProducts.Sum(p => p.UnitPrice * p.QuantityOnHand);

        var recentMovements = (await _inventoryService.GetMovementsAsync(limit: 8)).ToList();

        var lowStockDtos = lowStockProducts.Select(p => new ProductDto(
            p.Id,
            p.Sku,
            p.Name,
            p.Description,
            p.Category,
            p.UnitPrice,
            p.QuantityOnHand,
            p.ReorderLevel,
            p.IsActive,
            true,
            p.CreatedAtUtc
        )).ToList();

        return new DashboardKpisDto(
            totalProducts,
            lowStockCount,
            totalOrders,
            totalInventoryValue,
            lowStockDtos,
            recentMovements
        );
    }
}

