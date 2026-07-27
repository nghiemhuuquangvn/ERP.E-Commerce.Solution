using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class InventoryLocationStockRepository : IInventoryLocationStockRepository
{
    private readonly WarehouseDbContext _context;

    public InventoryLocationStockRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryLocationStock?> GetAsync(Guid productId, Guid locationId, CancellationToken cancellationToken)
        => await _context.InventoryLocationStocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.LocationId == locationId, cancellationToken);

    public async Task<List<InventoryLocationStock>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
        => await _context.InventoryLocationStocks
            .Where(s => s.ProductId == productId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(InventoryLocationStock stock, CancellationToken cancellationToken)
        => await _context.InventoryLocationStocks.AddAsync(stock, cancellationToken);

    public Task UpdateAsync(InventoryLocationStock stock, CancellationToken cancellationToken)
    {
        _context.InventoryLocationStocks.Update(stock);
        return Task.CompletedTask;
    }
}
