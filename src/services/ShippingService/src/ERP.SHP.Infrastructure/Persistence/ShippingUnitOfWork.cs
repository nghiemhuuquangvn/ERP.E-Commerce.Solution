using ERP.SHP.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.SHP.Infrastructure.Persistence;

public class ShippingUnitOfWork : IShippingUnitOfWork
{
    private readonly ShippingDbContext _dbContext;
    private IDbContextTransaction? _currentTrans;

    public ShippingUnitOfWork(
        ShippingDbContext dbContext,
        IShipmentRepository shipments,
        ICarrierRepository carriers,
        IReturnShipmentRepository returnShipments)
    {
        _dbContext = dbContext;
        Shipments = shipments;
        Carriers = carriers;
        ReturnShipments = returnShipments;
    }

    public IShipmentRepository Shipments { get; }
    public ICarrierRepository Carriers { get; }
    public IReturnShipmentRepository ReturnShipments { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await _dbContext.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default) =>
        _currentTrans = await _dbContext.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            if (_currentTrans != null) await _currentTrans.CommitAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            if (_currentTrans != null)
            {
                _currentTrans.Dispose();
                _currentTrans = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTrans != null)
        {
            await _currentTrans.RollbackAsync(ct);
            _currentTrans.Dispose();
            _currentTrans = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentTrans?.Dispose();
            _dbContext.Dispose();
        }
    }
}
