using ERP.SHP.Domain.Entities;

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
}

public interface ICarrierRepository
{
    Task<Carrier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Carrier>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(Carrier carrier, CancellationToken ct = default);
    Task UpdateAsync(Carrier carrier, CancellationToken ct = default);
}

public interface IReturnShipmentRepository
{
    Task<ReturnShipment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ReturnShipment>> GetByOriginalShipmentIdAsync(Guid originalShipmentId, CancellationToken ct = default);
    Task AddAsync(ReturnShipment returnShipment, CancellationToken ct = default);
    Task UpdateAsync(ReturnShipment returnShipment, CancellationToken ct = default);
}
