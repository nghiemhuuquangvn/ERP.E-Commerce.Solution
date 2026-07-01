using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class CountryRepository : ICountryRepository
{
    private readonly MasterDataDbContext _context;

    public CountryRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Countries
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Country> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Countries
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Countries
            .AnyAsync(c => !c.IsDeleted && c.Code == code, cancellationToken);
    }

    public async Task<CountryTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.CountryTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(Country country)
    {
        _context.Countries.Add(country);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
