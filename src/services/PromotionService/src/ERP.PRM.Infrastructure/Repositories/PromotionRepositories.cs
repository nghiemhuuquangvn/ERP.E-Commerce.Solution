using ERP.PRM.Application.Interfaces;
using ERP.PRM.Domain.Entities;
using ERP.PRM.Domain.Enums;
using ERP.PRM.Infrastructure.Models;
using ERP.PRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRM.Infrastructure.Repositories;

public class PromotionRepository : IPromotionRepository
{
    private readonly PromotionDbContext _db;
    public PromotionRepository(PromotionDbContext db) => _db = db;

    public async Task<Promotion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Promotions.Include(p => p.Coupons).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<Promotion>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.Promotions
            .Where(p => p.Status == PromotionStatus.Active && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
            .AsNoTracking().ToListAsync(ct);

    public async Task<List<Promotion>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Promotions.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Promotion promotion, CancellationToken ct = default)
    {
        await _db.Promotions.AddAsync(promotion, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Promotion promotion, CancellationToken ct = default)
    {
        _db.Promotions.Update(promotion);
        await _db.SaveChangesAsync(ct);
    }
}

public class CouponRepository : ICouponRepository
{
    private readonly PromotionDbContext _db;
    public CouponRepository(PromotionDbContext db) => _db = db;

    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await _db.Promotions.Include(p => p.Coupons)
            .SelectMany(p => p.Coupons)
            .FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task UpdateAsync(Coupon coupon, CancellationToken ct = default)
    {
        _db.Entry(coupon).State = EntityState.Modified;
        await _db.SaveChangesAsync(ct);
    }
}

public class LoyaltyAccountRepository : ILoyaltyAccountRepository
{
    private readonly PromotionDbContext _db;
    public LoyaltyAccountRepository(PromotionDbContext db) => _db = db;

    public async Task<LoyaltyAccount?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.LoyaltyAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

    public async Task AddAsync(LoyaltyAccount account, CancellationToken ct = default)
    {
        await _db.LoyaltyAccounts.AddAsync(account, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LoyaltyAccount account, CancellationToken ct = default)
    {
        _db.LoyaltyAccounts.Update(account);
        await _db.SaveChangesAsync(ct);
    }
}

public class PromotionProductCacheRepository : IPromotionProductCacheRepository
{
    private readonly PromotionDbContext _db;
    public PromotionProductCacheRepository(PromotionDbContext db) => _db = db;

    public async Task UpsertAsync(Guid productId, string category, decimal price, CancellationToken ct = default)
    {
        var existing = await _db.ProductCache.FindAsync(new object[] { productId }, ct);
        if (existing == null)
            await _db.ProductCache.AddAsync(new PromotionProductCache
            {
                ProductId = productId,
                Category = category,
                Price = price,
                SyncedAt = DateTime.UtcNow
            }, ct);
        else
        {
            existing.Category = category;
            existing.Price = price;
            existing.SyncedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetCategoryAsync(Guid productId, CancellationToken ct = default)
    {
        var entry = await _db.ProductCache.FindAsync(new object[] { productId }, ct);
        return entry?.Category;
    }
}

public class FlashSaleRepository : IFlashSaleRepository
{
    private readonly PromotionDbContext _db;
    public FlashSaleRepository(PromotionDbContext db) => _db = db;

    public async Task<FlashSale?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.FlashSales.Include(fs => fs.Items).FirstOrDefaultAsync(fs => fs.Id == id, ct);

    public async Task<List<FlashSale>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.FlashSales
            .Include(fs => fs.Items)
            .Where(fs => fs.Status == FlashSaleStatus.Active && fs.StartTime <= DateTime.UtcNow && fs.EndTime >= DateTime.UtcNow)
            .AsNoTracking().ToListAsync(ct);

    public async Task<List<FlashSale>> GetAllAsync(CancellationToken ct = default) =>
        await _db.FlashSales.Include(fs => fs.Items).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(FlashSale flashSale, CancellationToken ct = default)
    {
        await _db.FlashSales.AddAsync(flashSale, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FlashSale flashSale, CancellationToken ct = default)
    {
        _db.FlashSales.Update(flashSale);
        await _db.SaveChangesAsync(ct);
    }
}
