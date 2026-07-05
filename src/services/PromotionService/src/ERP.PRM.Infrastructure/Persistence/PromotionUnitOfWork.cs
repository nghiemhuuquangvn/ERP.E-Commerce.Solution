using ERP.PRM.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.PRM.Infrastructure.Persistence;

public class PromotionUnitOfWork : IPromotionUnitOfWork
{
    private readonly PromotionDbContext _dbContext;
    private IDbContextTransaction? _currentTrans;

    public PromotionUnitOfWork(
        PromotionDbContext dbContext,
        IPromotionRepository promotions,
        ICouponRepository coupons,
        ILoyaltyAccountRepository loyaltyAccounts,
        IFlashSaleRepository flashSales,
        IPromotionProductCacheRepository productCache)
    {
        _dbContext = dbContext;
        Promotions = promotions;
        Coupons = coupons;
        LoyaltyAccounts = loyaltyAccounts;
        FlashSales = flashSales;
        ProductCache = productCache;
    }

    public IPromotionRepository Promotions { get; }
    public ICouponRepository Coupons { get; }
    public ILoyaltyAccountRepository LoyaltyAccounts { get; }
    public IFlashSaleRepository FlashSales { get; }
    public IPromotionProductCacheRepository ProductCache { get; }

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
