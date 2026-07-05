using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Locations;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class LocationRepository : ILocationRepository
{
    private readonly WarehouseDbContext _context;

    public LocationRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<WarehouseLocation?> GetByIdAsync(Guid locationId, CancellationToken cancellationToken)
    {
        return await _context.WarehouseLocations.FirstOrDefaultAsync(l => l.LocationId == locationId, cancellationToken);
    }

    public async Task<WarehouseLocation?> GetByCodeAsync(string locationCode, CancellationToken cancellationToken)
    {
        return await _context.WarehouseLocations.FirstOrDefaultAsync(l => l.LocationCode == locationCode, cancellationToken);
    }

    public async Task<List<WarehouseLocation>> GetAllAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var query = _context.WarehouseLocations.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(l => l.IsActive == isActive.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WarehouseLocation location, CancellationToken cancellationToken)
    {
        await _context.WarehouseLocations.AddAsync(location, cancellationToken);
    }

    public async Task UpdateAsync(WarehouseLocation location, CancellationToken cancellationToken)
    {
        _context.WarehouseLocations.Update(location);
    }
}
