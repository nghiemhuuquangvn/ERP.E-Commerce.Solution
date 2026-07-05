using ERP.WH.Domain.Aggregates.Stocktakes;
using ERP.WH.Application.Commands.Stocktakes;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class StocktakeRepository : IStocktakeRepository
{
    private readonly WarehouseDbContext _context;

    public StocktakeRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<Stocktake?> GetByIdAsync(Guid stocktakeId, CancellationToken cancellationToken)
    {
        return await _context.Stocktakes.FirstOrDefaultAsync(s => s.StocktakeId == stocktakeId, cancellationToken);
    }

    public async Task<List<Stocktake>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Stocktakes.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Stocktake stocktake, CancellationToken cancellationToken)
    {
        await _context.Stocktakes.AddAsync(stocktake, cancellationToken);
    }

    public async Task UpdateAsync(Stocktake stocktake, CancellationToken cancellationToken)
    {
        _context.Stocktakes.Update(stocktake);
    }
}
