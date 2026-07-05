using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly WarehouseDbContext _context;

    public StockMovementRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<StockMovement> GetByIdAsync(Guid movementId, CancellationToken cancellationToken)
    {
        return await _context.StockMovements.FirstOrDefaultAsync(m => m.MovementId == movementId, cancellationToken);
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        await _context.StockMovements.AddAsync(movement, cancellationToken);
    }

    public async Task<List<StockMovement>> GetByProductIdAsync(
        Guid productId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var query = _context.StockMovements.Where(m => m.ProductId == productId);

        if (startDate.HasValue)
            query = query.Where(m => m.RecordedAt >= startDate);

        if (endDate.HasValue)
            query = query.Where(m => m.RecordedAt <= endDate);

        return await query.ToListAsync(cancellationToken);
    }
}
