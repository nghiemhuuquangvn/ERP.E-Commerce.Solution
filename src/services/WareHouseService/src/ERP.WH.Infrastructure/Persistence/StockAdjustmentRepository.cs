using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly WarehouseDbContext _context;

    public StockAdjustmentRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _context.StockAdjustments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<List<StockAdjustment>> ListAsync(StockAdjustmentStatus? status, CancellationToken cancellationToken)
    {
        var query = _context.StockAdjustments.AsQueryable();
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken)
        => await _context.StockAdjustments.AddAsync(adjustment, cancellationToken);

    public Task UpdateAsync(StockAdjustment adjustment, CancellationToken cancellationToken)
    {
        _context.StockAdjustments.Update(adjustment);
        return Task.CompletedTask;
    }
}
