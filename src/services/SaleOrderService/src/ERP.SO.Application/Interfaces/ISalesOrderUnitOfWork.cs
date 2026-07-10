using ERP.SO.Application.Handlers;

namespace ERP.SO.Application.Interfaces;

public interface ISalesOrderUnitOfWork : IDisposable
{
    ISalesOrderRepository Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
