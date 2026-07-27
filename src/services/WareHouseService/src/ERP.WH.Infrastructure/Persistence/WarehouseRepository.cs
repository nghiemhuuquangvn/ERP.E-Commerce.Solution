using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Location;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly WarehouseDbContext _context;

    public WarehouseRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<List<Warehouse>> GetAllAsync(CancellationToken cancellationToken)
        => await _context.Warehouses.ToListAsync(cancellationToken);

    public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken)
        => await _context.Warehouses.AddAsync(warehouse, cancellationToken);

    public Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        _context.Warehouses.Update(warehouse);
        return Task.CompletedTask;
    }
}
