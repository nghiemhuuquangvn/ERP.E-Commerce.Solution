namespace ERP.PRM.Application.Interfaces;

public interface IPromotionUnitOfWork : IDisposable
{
    IPromotionRepository Promotions { get; }
    ICouponRepository Coupons { get; }
    ILoyaltyAccountRepository LoyaltyAccounts { get; }
    IFlashSaleRepository FlashSales { get; }
    IPromotionProductCacheRepository ProductCache { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
