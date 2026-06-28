using ERP.POS.Application.EventHandlers;
using ERP.POS.Application.Interfaces;
using ERP.POS.Domain.Entities;
using ERP.POS.Domain.Enums;
using ERP.POS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.POS.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly POSDbContext _db;
    public TransactionRepository(POSDbContext db) => _db = db;

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Transactions.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<Transaction>> GetByShiftAsync(Guid shiftId, CancellationToken ct = default) =>
        await _db.Transactions.Include(t => t.Items)
            .Where(t => t.CashShiftId == shiftId).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await _db.Transactions.AddAsync(transaction, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _db.Transactions.Update(transaction);
        await _db.SaveChangesAsync(ct);
    }
}

public class CashShiftRepository : ICashShiftRepository
{
    private readonly POSDbContext _db;
    public CashShiftRepository(POSDbContext db) => _db = db;

    public async Task<CashShift?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.CashShifts.FindAsync(new object[] { id }, ct);

    public async Task<CashShift?> GetOpenByTerminalAsync(Guid terminalId, CancellationToken ct = default) =>
        await _db.CashShifts.FirstOrDefaultAsync(s => s.TerminalId == terminalId && s.Status == CashShiftStatus.Open, ct);

    public async Task AddAsync(CashShift shift, CancellationToken ct = default)
    {
        await _db.CashShifts.AddAsync(shift, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CashShift shift, CancellationToken ct = default)
    {
        _db.CashShifts.Update(shift);
        await _db.SaveChangesAsync(ct);
    }
}

public class PosProductCacheRepository : IPosProductCacheRepository
{
    private readonly POSDbContext _db;
    public PosProductCacheRepository(POSDbContext db) => _db = db;

    public async Task UpsertAsync(PosProductCache product, CancellationToken ct = default)
    {
        var existing = await _db.ProductCache.FindAsync(new object[] { product.ProductId }, ct);
        if (existing == null)
            await _db.ProductCache.AddAsync(product, ct);
        else
        {
            existing.Name = product.Name;
            existing.Sku = product.Sku;
            existing.Price = product.Price;
            existing.IsActive = product.IsActive;
            existing.SyncedAt = product.SyncedAt;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdatePriceAsync(Guid productId, decimal newPrice, CancellationToken ct = default)
    {
        var existing = await _db.ProductCache.FindAsync(new object[] { productId }, ct);
        if (existing == null) return;
        existing.Price = newPrice;
        existing.SyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PosProductCache?> GetBySkuAsync(string sku, CancellationToken ct = default) =>
        await _db.ProductCache.FirstOrDefaultAsync(p => p.Sku == sku, ct);

    public async Task<List<PosProductCache>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.ProductCache.Where(p => p.IsActive).AsNoTracking().ToListAsync(ct);
}

public class PosPromotionCacheRepository : IPosPromotionCacheRepository
{
    private readonly POSDbContext _db;
    public PosPromotionCacheRepository(POSDbContext db) => _db = db;

    public async Task UpsertAsync(PosPromotionCache promotion, CancellationToken ct = default)
    {
        var existing = await _db.PromotionCache.FindAsync(new object[] { promotion.PromotionId }, ct);
        if (existing == null)
            await _db.PromotionCache.AddAsync(promotion, ct);
        else
        {
            existing.Name = promotion.Name;
            existing.DiscountType = promotion.DiscountType;
            existing.DiscountValue = promotion.DiscountValue;
            existing.StartDate = promotion.StartDate;
            existing.EndDate = promotion.EndDate;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<PosPromotionCache>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.PromotionCache
            .Where(p => p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
            .AsNoTracking().ToListAsync(ct);
}
