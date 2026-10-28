using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;

namespace ERP.SHP.Application.Interfaces;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Shipment>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Tracked (not AsNoTracking) variant for callers that will mutate and persist —
    /// GetByOrderIdAsync is AsNoTracking for read-only queries, and reattaching a
    /// no-tracking-loaded aggregate via Update() after adding a new owned child (e.g.
    /// a TrackingEvent, whose Id is already a non-default Guid) risks EF guessing
    /// "Modified" instead of "Added" for that child.
    /// </summary>
    Task<List<Shipment>> GetTrackedByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    Task AddAsync(Shipment shipment, CancellationToken ct = default);
    Task UpdateAsync(Shipment shipment, CancellationToken ct = default);

    /// <summary>Tracked, for a carrier notification or poll that will change it. Null when no shipment of the current tenant has this tracking code.</summary>
    Task<(List<Shipment> Items, int Total)> ListAsync(int skip, int take, ShipmentStatus? status, string? search, bool needsReview, CancellationToken ct = default);

    Task<Shipment?> GetTrackedByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default);

    /// <summary>The tenant that owns a shipment with this tracking code, across tenants (the webhook has no tenant until it is found).</summary>
    Task<Guid?> FindTenantByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default);

    /// <summary>Shipments a carrier is still handling (not delivered / returned / cancelled) that have not been updated since <paramref name="before"/>.</summary>
    Task<List<Shipment>> GetStaleInCarrierHandsAsync(DateTime before, int take, CancellationToken ct = default);

    /// <summary>Tenants that have at least one shipment in carrier hands (cross-tenant, ids only).</summary>
    Task<List<Guid>> GetTenantsWithOpenShipmentsAsync(CancellationToken ct = default);

    Task<bool> WebhookSeenAsync(string carrierCode, string trackingCode, string carrierStatus, DateTime actionTime, CancellationToken ct = default);
    Task AddWebhookLogAsync(CarrierWebhookLog log, CancellationToken ct = default);
}

public interface ICarrierRepository
{
    Task<Carrier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Carrier>> GetActiveAsync(CancellationToken ct = default);
    /// <summary>The shop's carrier row for a carrier code (GHTK, VTP, GHN, JNT); null when it has none yet.</summary>
    Task<Carrier?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(Carrier carrier, CancellationToken ct = default);
    Task UpdateAsync(Carrier carrier, CancellationToken ct = default);
}

/// <summary>Selection rules and the shop's shipping settings (pickup address, default item weight) for the booking.</summary>
public interface IShippingConfigRepository
{
    Task<List<CarrierSelectionRule>> GetActiveRulesAsync(CancellationToken ct = default);
    /// <summary>The shop's delivery price list (active rows), in the order they are tried.</summary>
    Task<List<ERP.SHP.Domain.Entities.ShippingFeeRule>> GetFeeRulesAsync(CancellationToken ct = default);
    Task<ERP.SHP.Domain.Entities.Settings.TenantShippingSettings?> GetTenantSettingsAsync(CancellationToken ct = default);
}

public interface IReturnShipmentRepository
{
    Task<ReturnShipment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ReturnShipment>> GetByOriginalShipmentIdAsync(Guid originalShipmentId, CancellationToken ct = default);
    Task AddAsync(ReturnShipment returnShipment, CancellationToken ct = default);
    Task UpdateAsync(ReturnShipment returnShipment, CancellationToken ct = default);
}
