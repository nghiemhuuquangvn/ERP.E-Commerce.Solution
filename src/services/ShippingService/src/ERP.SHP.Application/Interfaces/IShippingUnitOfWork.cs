namespace ERP.SHP.Application.Interfaces;

public interface IShippingUnitOfWork : IDisposable
{
    IShipmentRepository Shipments { get; }
    ICarrierRepository Carriers { get; }
    IReturnShipmentRepository ReturnShipments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Writes an integration event straight into the outbox (stamped with the current tenant) and saves, for events no aggregate raises.</summary>
    Task StageEventAsync(ERP.Shared.Core.Domain.IDomainEvent integrationEvent, CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
