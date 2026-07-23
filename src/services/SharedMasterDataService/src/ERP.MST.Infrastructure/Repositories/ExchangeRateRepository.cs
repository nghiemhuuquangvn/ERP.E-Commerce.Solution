using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly MasterDataDbContext _context;

    public ExchangeRateRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<ExchangeRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRates
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<ExchangeRate> Items, int TotalCount)> ListAsync(
        Guid? fromCurrencyId,
        Guid? toCurrencyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ExchangeRates
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (fromCurrencyId.HasValue)
            query = query.Where(e => e.FromCurrencyId == fromCurrencyId.Value);

        if (toCurrencyId.HasValue)
            query = query.Where(e => e.ToCurrencyId == toCurrencyId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.EffectiveDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    // Business rule (docs/business/MasterData/module-03-master-data.md §8): the
    // exchange rate record with the latest EffectiveDate <= asOfDate.
    public async Task<ExchangeRate?> GetEffectiveRateAsync(
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRates
            .Where(e => !e.IsDeleted
                && e.FromCurrencyId == fromCurrencyId
                && e.ToCurrencyId == toCurrencyId
                && e.EffectiveDate <= asOfDate)
            .OrderByDescending(e => e.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(ExchangeRate exchangeRate)
    {
        _context.ExchangeRates.Add(exchangeRate);
    }

    public void Update(ExchangeRate exchangeRate)
    {
        _context.ExchangeRates.Update(exchangeRate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
