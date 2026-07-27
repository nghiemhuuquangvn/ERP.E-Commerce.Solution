using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class InventoryValuationSnapshotRepository : IInventoryValuationSnapshotRepository
{
    private readonly WarehouseDbContext _context;

    public InventoryValuationSnapshotRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryValuationSnapshot?> GetByPeriodAsync(string periodCode, Guid? warehouseId, CancellationToken cancellationToken)
    {
        return await _context.InventoryValuationSnapshots
            .FirstOrDefaultAsync(s => s.PeriodCode == periodCode && s.WarehouseId == warehouseId, cancellationToken);
    }

    public async Task AddAsync(InventoryValuationSnapshot snapshot, CancellationToken cancellationToken)
    {
        await _context.InventoryValuationSnapshots.AddAsync(snapshot, cancellationToken);
    }
}
