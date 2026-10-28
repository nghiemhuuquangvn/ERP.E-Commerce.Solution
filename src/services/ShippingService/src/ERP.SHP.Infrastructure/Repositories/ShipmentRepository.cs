using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
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

    public async Task<(List<Shipment> Items, int Total)> ListAsync(int skip, int take, ShipmentStatus? status, string? search, bool needsReview, CancellationToken ct = default)
    {
        var query = _db.Shipments.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (needsReview) query = query.Where(s => s.ReviewNote != null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(s => s.TrackingNumber == q || s.CarrierOrderCode == q || s.RecipientName.Contains(q));
        }
        var total = await query.CountAsync(ct);
        var items = await query.Include(s => s.Items).Include(s => s.TrackingEvents).OrderByDescending(s => s.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<Shipment?> GetTrackedByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default) =>
        await _db.Shipments.Include(s => s.Items).Include(s => s.TrackingEvents)
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, ct);

    public async Task<Guid?> FindTenantByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default) =>
        await _db.Shipments
            .IgnoreQueryFilters() // deliberate: a carrier notification has no tenant until its shipment is found
            .Where(s => s.TrackingNumber == trackingNumber)
            .Select(s => (Guid?)s.TenantId)
            .FirstOrDefaultAsync(ct)
        // a carrier also reports the reverse parcel of a return (P1-11d): the same notification path finds its shop
        ?? await _db.ReturnShipments.IgnoreQueryFilters()
            .Where(r => r.TrackingNumber == trackingNumber)
            .Select(r => (Guid?)r.TenantId)
            .FirstOrDefaultAsync(ct);

    private static readonly ShipmentStatus[] Final = { ShipmentStatus.Delivered, ShipmentStatus.Returned, ShipmentStatus.Cancelled };

    public async Task<List<Shipment>> GetStaleInCarrierHandsAsync(DateTime before, int take, CancellationToken ct = default) =>
        await _db.Shipments.Include(s => s.TrackingEvents)
            .Where(s => s.TrackingNumber != null && !Final.Contains(s.Status) && (s.LastCarrierSyncAt ?? s.CreatedAt) < before)
            .OrderBy(s => s.LastCarrierSyncAt ?? s.CreatedAt).Take(take).ToListAsync(ct);

    public async Task<List<Guid>> GetTenantsWithOpenShipmentsAsync(CancellationToken ct = default) =>
        await _db.Shipments.IgnoreQueryFilters() // deliberate: cross-tenant job, tenant ids only
            .Where(s => s.TrackingNumber != null && !Final.Contains(s.Status)).Select(s => s.TenantId).Distinct().ToListAsync(ct);

    public async Task<bool> WebhookSeenAsync(string carrierCode, string trackingCode, string carrierStatus, DateTime actionTime, CancellationToken ct = default) =>
        await _db.CarrierWebhookLogs.AnyAsync(l => l.CarrierCode == carrierCode && l.TrackingCode == trackingCode && l.CarrierStatus == carrierStatus && l.ActionTime == actionTime, ct);

    public async Task AddWebhookLogAsync(CarrierWebhookLog log, CancellationToken ct = default) =>
        await _db.CarrierWebhookLogs.AddAsync(log, ct);
}

public class ReturnShipmentRepository : IReturnShipmentRepository
{
    private readonly ShippingDbContext _db;
    public ReturnShipmentRepository(ShippingDbContext db) => _db = db;

    public async Task<ReturnShipment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.ReturnShipments.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<List<ReturnShipment>> GetByOriginalShipmentIdAsync(Guid originalShipmentId, CancellationToken ct = default) =>
        await _db.ReturnShipments.Where(r => r.OriginalShipmentId == originalShipmentId).AsNoTracking().ToListAsync(ct);

    public async Task<List<ReturnShipment>> GetTrackedByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await _db.ReturnShipments.Where(r => r.OrderId == orderId).ToListAsync(ct);

    public async Task<ReturnShipment?> GetTrackedByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default) =>
        await _db.ReturnShipments.FirstOrDefaultAsync(r => r.TrackingNumber == trackingNumber, ct);

    private static readonly ReturnShipmentStatus[] FinalReturn = { ReturnShipmentStatus.Received, ReturnShipmentStatus.Cancelled };

    public async Task<List<ReturnShipment>> GetStaleInCarrierHandsAsync(DateTime before, int take, CancellationToken ct = default) =>
        await _db.ReturnShipments
            .Where(r => r.TrackingNumber != null && r.CarrierId != null && !FinalReturn.Contains(r.Status) && (r.LastCarrierSyncAt ?? r.CreatedAt) < before)
            .OrderBy(r => r.LastCarrierSyncAt ?? r.CreatedAt).Take(take).ToListAsync(ct);

    public async Task<List<Guid>> GetTenantsWithOpenReturnsAsync(CancellationToken ct = default) =>
        await _db.ReturnShipments.IgnoreQueryFilters() // deliberate: cross-tenant job, tenant ids only
            .Where(r => r.TrackingNumber != null && r.CarrierId != null && !FinalReturn.Contains(r.Status)).Select(r => r.TenantId).Distinct().ToListAsync(ct);

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

    public async Task<List<ERP.SHP.Domain.Entities.ShippingFeeRule>> GetFeeRulesAsync(CancellationToken ct = default) =>
        await _db.ShippingFeeRules.Where(r => r.IsActive).OrderBy(r => r.Priority).ThenBy(r => r.Name).AsNoTracking().ToListAsync(ct);

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
