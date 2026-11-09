using ERP.WH.Application.Interfaces;
using ERP.WH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Services;

/// <summary>Scoped: the default warehouse is read once per request / message. Reads go through the tenant query filter of the context.</summary>
public class WarehouseResolver(WarehouseDbContext context) : IWarehouseResolver
{
    private bool _defaultKnown;
    private Guid? _default;

    public async Task<Guid?> ForLocationAsync(Guid locationId, CancellationToken cancellationToken)
        => await context.WarehouseLocations.Where(l => l.LocationId == locationId).Select(l => l.WarehouseId).FirstOrDefaultAsync(cancellationToken);

    public async Task<Guid?> DefaultAsync(CancellationToken cancellationToken)
    {
        if (_defaultKnown) return _default;

        var configured = await context.TenantInventorySettings.Select(s => s.DefaultWarehouseId).FirstOrDefaultAsync(cancellationToken);
        if (configured.HasValue && await context.Warehouses.AnyAsync(w => w.Id == configured.Value && w.IsActive, cancellationToken))
            _default = configured;
        else
            _default = await context.Warehouses.Where(w => w.IsActive)
                .OrderBy(w => w.Code == "MAIN" ? 0 : 1).ThenBy(w => w.Code)
                .Select(w => (Guid?)w.Id).FirstOrDefaultAsync(cancellationToken);

        _defaultKnown = true;
        return _default;
    }
}
