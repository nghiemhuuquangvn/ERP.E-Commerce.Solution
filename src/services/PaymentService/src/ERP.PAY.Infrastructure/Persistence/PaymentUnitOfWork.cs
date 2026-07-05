using ERP.PAY.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.PAY.Infrastructure.Persistence;

public class PaymentUnitOfWork : IPaymentUnitOfWork
{
    private readonly PaymentDbContext _dbContext;
    private IDbContextTransaction? _currentTrans;

    public PaymentUnitOfWork(
        PaymentDbContext dbContext,
        IPaymentRepository payments,
        ISettlementRepository settlements,
        ISavedPaymentMethodRepository savedPaymentMethods,
        IInstallmentPlanRepository installmentPlans)
    {
        _dbContext = dbContext;
        Payments = payments;
        Settlements = settlements;
        SavedPaymentMethods = savedPaymentMethods;
        InstallmentPlans = installmentPlans;
    }

    public IPaymentRepository Payments { get; }
    public ISettlementRepository Settlements { get; }
    public ISavedPaymentMethodRepository SavedPaymentMethods { get; }
    public IInstallmentPlanRepository InstallmentPlans { get; }

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
