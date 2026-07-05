using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class InventoryLotRepository : IInventoryLotRepository
{
    private readonly WarehouseDbContext _context;

    public InventoryLotRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryLot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken)
    {
        return await _context.InventoryLots.FirstOrDefaultAsync(l => l.LotId == lotId, cancellationToken);
    }

    public async Task<List<InventoryLot>> GetByProductIdAsync(Guid productId, bool includeDepleted, CancellationToken cancellationToken)
    {
        var query = _context.InventoryLots.Where(l => l.ProductId == productId);

        if (!includeDepleted)
            query = query.Where(l => l.RemainingQuantity > 0);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<InventoryLot>> GetExpiringAsync(DateTime beforeUtc, CancellationToken cancellationToken)
    {
        return await _context.InventoryLots
            .Where(l => l.RemainingQuantity > 0 && l.ExpiryDate != null && l.ExpiryDate <= beforeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(InventoryLot lot, CancellationToken cancellationToken)
    {
        await _context.InventoryLots.AddAsync(lot, cancellationToken);
    }

    public async Task UpdateAsync(InventoryLot lot, CancellationToken cancellationToken)
    {
        _context.InventoryLots.Update(lot);
    }
}
