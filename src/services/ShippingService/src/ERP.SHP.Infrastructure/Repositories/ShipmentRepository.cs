using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.SHP.Infrastructure.Repositories;

public class ShipmentRepository : IShipmentRepository
{
    private readonly ShippingDbContext _db;
    public ShipmentRepository(ShippingDbContext db) => _db = db;

    public async Task<Shipment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Shipments.Include(s => s.Items).Include(s => s.TrackingEvents)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<List<Shipment>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await _db.Shipments.Include(s => s.Items).Include(s => s.TrackingEvents)
            .Where(s => s.OrderId == orderId).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Shipment shipment, CancellationToken ct = default)
    {
        await _db.Shipments.AddAsync(shipment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Shipment shipment, CancellationToken ct = default)
    {
        _db.Shipments.Update(shipment);
        await _db.SaveChangesAsync(ct);
    }
}

public class CarrierRepository : ICarrierRepository
{
    private readonly ShippingDbContext _db;
    public CarrierRepository(ShippingDbContext db) => _db = db;

    public async Task<Carrier?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Carriers.FindAsync(new object[] { id }, ct);

    public async Task<List<Carrier>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.Carriers.Where(c => c.IsActive).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Carrier carrier, CancellationToken ct = default)
    {
        await _db.Carriers.AddAsync(carrier, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Carrier carrier, CancellationToken ct = default)
    {
        _db.Carriers.Update(carrier);
        await _db.SaveChangesAsync(ct);
    }
}
