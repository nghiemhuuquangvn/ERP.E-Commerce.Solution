using ERP.POS.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.POS.Infrastructure.Persistence;

public class PosUnitOfWork : IPosUnitOfWork
{
    private readonly POSDbContext _dbContext;
    private IDbContextTransaction? _currentTrans;

    public PosUnitOfWork(
        POSDbContext dbContext,
        ITransactionRepository transactions,
        ICashShiftRepository cashShifts,
        ITerminalRepository terminals,
        IPosProductCacheRepository productCache,
        IPosPromotionCacheRepository promotionCache)
    {
        _dbContext = dbContext;
        Transactions = transactions;
        CashShifts = cashShifts;
        Terminals = terminals;
        ProductCache = productCache;
        PromotionCache = promotionCache;
    }

    public ITransactionRepository Transactions { get; }
    public ICashShiftRepository CashShifts { get; }
    public ITerminalRepository Terminals { get; }
    public IPosProductCacheRepository ProductCache { get; }
    public IPosPromotionCacheRepository PromotionCache { get; }

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
