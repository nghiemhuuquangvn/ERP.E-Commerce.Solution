using ERP.SO.Application.Handlers;
using ERP.SO.Application.Sagas;

namespace ERP.SO.Application.Interfaces;

public interface ISalesOrderUnitOfWork : IDisposable
{
    ISalesOrderRepository Orders { get; }
    IOrderFulfillmentSagaRepository Sagas { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
