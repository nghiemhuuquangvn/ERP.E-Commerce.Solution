using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.SO.Infrastructure;

public sealed class SalesOrderUnitOfWork : ISalesOrderUnitOfWork
{
    private readonly SalesOrderDbContext _db;
    private IDbContextTransaction? _transaction;

    public SalesOrderUnitOfWork(
        SalesOrderDbContext db,
        ISalesOrderRepository orders,
        IOrderFulfillmentSagaRepository sagas)
    {
        _db = db;
        Orders = orders;
        Sagas = sagas;
    }

    public ISalesOrderRepository Orders { get; }
    public IOrderFulfillmentSagaRepository Sagas { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _db.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            if (_transaction != null) await _transaction.CommitAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
        finally { _transaction?.Dispose(); _transaction = null; }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync(ct);
        _transaction.Dispose();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
