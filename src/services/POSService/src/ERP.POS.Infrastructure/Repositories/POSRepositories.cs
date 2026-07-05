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

    public async Task<Transaction?> GetOpenByTerminalAsync(Guid terminalId, CancellationToken ct = default) =>
        await _db.Transactions.Include(t => t.Items)
            .Where(t => t.TerminalId == terminalId && t.Status == TransactionStatus.Open)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default) =>
        await _db.Transactions.AddAsync(transaction, ct);

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _db.Transactions.Update(transaction);
        return Task.CompletedTask;
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

    public async Task<List<CashShift>> GetByDateRangeAsync(Guid? terminalId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var query = _db.CashShifts.Where(s => s.OpenedAt >= start && s.OpenedAt <= end);

        if (terminalId.HasValue)
            query = query.Where(s => s.TerminalId == terminalId.Value);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(CashShift shift, CancellationToken ct = default) =>
        await _db.CashShifts.AddAsync(shift, ct);

    public Task UpdateAsync(CashShift shift, CancellationToken ct = default)
    {
        _db.CashShifts.Update(shift);
        return Task.CompletedTask;
    }
}

public class TerminalRepository : ITerminalRepository
{
    private readonly POSDbContext _db;
    public TerminalRepository(POSDbContext db) => _db = db;

    public async Task<Terminal?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Terminals.FindAsync(new object[] { id }, ct);

    public async Task<List<Terminal>> GetAllAsync(bool? isActive, CancellationToken ct = default)
    {
        var query = _db.Terminals.AsQueryable();
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(Terminal terminal, CancellationToken ct = default) =>
        await _db.Terminals.AddAsync(terminal, ct);

    public Task UpdateAsync(Terminal terminal, CancellationToken ct = default)
    {
        _db.Terminals.Update(terminal);
        return Task.CompletedTask;
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
    }

    public async Task UpdatePriceAsync(Guid productId, decimal newPrice, CancellationToken ct = default)
    {
        var existing = await _db.ProductCache.FindAsync(new object[] { productId }, ct);
        if (existing == null) return;
        existing.Price = newPrice;
        existing.SyncedAt = DateTime.UtcNow;
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
    }

    public async Task<List<PosPromotionCache>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.PromotionCache
            .Where(p => p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
            .AsNoTracking().ToListAsync(ct);
}
