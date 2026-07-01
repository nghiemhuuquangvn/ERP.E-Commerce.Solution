using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class WardRepository : IWardRepository
{
    private readonly MasterDataDbContext _context;

    public WardRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Ward?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Wards
            .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Ward> Items, int TotalCount)> ListAsync(
        Guid districtId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Wards
            .Where(w => !w.IsDeleted && w.DistrictId == districtId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(w => w.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<WardTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.WardTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(Ward ward)
    {
        _context.Wards.Add(ward);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
