using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class CostConsumptionLogRepository : ICostConsumptionLogRepository
{
    private readonly WarehouseDbContext _context;

    public CostConsumptionLogRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CostConsumptionLog log, CancellationToken cancellationToken)
    {
        await _context.CostConsumptionLogs.AddAsync(log, cancellationToken);
    }

    public async Task<List<CostConsumptionLog>> GetByProductIdAsync(Guid productId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var query = _context.CostConsumptionLogs.Where(l => l.ProductId == productId);

        if (startDate.HasValue)
            query = query.Where(l => l.ConsumedAt >= startDate);

        if (endDate.HasValue)
            query = query.Where(l => l.ConsumedAt <= endDate);

        return await query.OrderByDescending(l => l.ConsumedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<CostConsumptionLog>> GetBySourceOrderIdAsync(Guid sourceOrderId, CancellationToken cancellationToken)
    {
        return await _context.CostConsumptionLogs
            .Where(l => l.SourceOrderId == sourceOrderId)
            .ToListAsync(cancellationToken);
    }
}
