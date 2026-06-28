using ERP.PRM.Domain.Entities;

namespace ERP.PRM.Application.Interfaces;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Promotion>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(Promotion promotion, CancellationToken ct = default);
    Task UpdateAsync(Promotion promotion, CancellationToken ct = default);
}

public interface ICouponRepository
{
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task UpdateAsync(Coupon coupon, CancellationToken ct = default);
}

public interface ILoyaltyAccountRepository
{
    Task<LoyaltyAccount?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(LoyaltyAccount account, CancellationToken ct = default);
    Task UpdateAsync(LoyaltyAccount account, CancellationToken ct = default);
}

public interface IPromotionProductCacheRepository
{
    Task UpsertAsync(Guid productId, string category, decimal price, CancellationToken ct = default);
    Task<string?> GetCategoryAsync(Guid productId, CancellationToken ct = default);
}
