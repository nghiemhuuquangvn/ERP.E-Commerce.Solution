namespace ERP.SHP.Application.Interfaces;

public interface IShippingUnitOfWork : IDisposable
{
    IShipmentRepository Shipments { get; }
    ICarrierRepository Carriers { get; }
    IReturnShipmentRepository ReturnShipments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
