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

    public async Task<List<Shipment>> GetTrackedByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await _db.Shipments.Include(s => s.Items).Include(s => s.TrackingEvents)
            .Where(s => s.OrderId == orderId).ToListAsync(ct);

    public async Task AddAsync(Shipment shipment, CancellationToken ct = default) =>
        await _db.Shipments.AddAsync(shipment, ct);

    public Task UpdateAsync(Shipment shipment, CancellationToken ct = default)
    {
        _db.Shipments.Update(shipment);
        return Task.CompletedTask;
    }
}

public class ReturnShipmentRepository : IReturnShipmentRepository
{
    private readonly ShippingDbContext _db;
    public ReturnShipmentRepository(ShippingDbContext db) => _db = db;

    public async Task<ReturnShipment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.ReturnShipments.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<List<ReturnShipment>> GetByOriginalShipmentIdAsync(Guid originalShipmentId, CancellationToken ct = default) =>
        await _db.ReturnShipments.Where(r => r.OriginalShipmentId == originalShipmentId).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(ReturnShipment returnShipment, CancellationToken ct = default) =>
        await _db.ReturnShipments.AddAsync(returnShipment, ct);

    public Task UpdateAsync(ReturnShipment returnShipment, CancellationToken ct = default)
    {
        _db.ReturnShipments.Update(returnShipment);
        return Task.CompletedTask;
    }
}

public class ShippingConfigRepository : IShippingConfigRepository
{
    private readonly ShippingDbContext _db;
    public ShippingConfigRepository(ShippingDbContext db) => _db = db;

    public async Task<List<CarrierSelectionRule>> GetActiveRulesAsync(CancellationToken ct = default) =>
        await _db.CarrierSelectionRules.Where(r => r.IsActive).AsNoTracking().ToListAsync(ct);

    public async Task<ERP.SHP.Domain.Entities.Settings.TenantShippingSettings?> GetTenantSettingsAsync(CancellationToken ct = default) =>
        await _db.TenantShippingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
}

public class CarrierRepository : ICarrierRepository
{
    private readonly ShippingDbContext _db;
    public CarrierRepository(ShippingDbContext db) => _db = db;

    public async Task<Carrier?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Carriers.FindAsync(new object[] { id }, ct);

    public async Task<List<Carrier>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.Carriers.Where(c => c.IsActive).AsNoTracking().ToListAsync(ct);

    public async Task<Carrier?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await _db.Carriers.FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task AddAsync(Carrier carrier, CancellationToken ct = default) =>
        await _db.Carriers.AddAsync(carrier, ct);

    public Task UpdateAsync(Carrier carrier, CancellationToken ct = default)
    {
        _db.Carriers.Update(carrier);
        return Task.CompletedTask;
    }
}
