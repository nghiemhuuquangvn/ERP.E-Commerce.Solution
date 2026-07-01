using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class ProvinceRepository : IProvinceRepository
{
    private readonly MasterDataDbContext _context;

    public ProvinceRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Province?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Provinces
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Province> Items, int TotalCount)> ListAsync(
        Guid countryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Provinces
            .Where(p => !p.IsDeleted && p.CountryId == countryId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ProvinceTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.ProvinceTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(Province province)
    {
        _context.Provinces.Add(province);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
