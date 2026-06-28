using ERP.SHP.Domain.Entities;

namespace ERP.SHP.Application.Interfaces;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Shipment>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
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
