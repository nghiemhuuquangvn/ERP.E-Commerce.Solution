using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class DistrictRepository : IDistrictRepository
{
    private readonly MasterDataDbContext _context;

    public DistrictRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<District?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Districts
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<District> Items, int TotalCount)> ListAsync(
        Guid provinceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Districts
            .Where(d => !d.IsDeleted && d.ProvinceId == provinceId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<DistrictTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.DistrictTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(District district)
    {
        _context.Districts.Add(district);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
