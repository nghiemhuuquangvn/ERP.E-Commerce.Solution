using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class UnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly MasterDataDbContext _context;

    public UnitOfMeasureRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<UnitOfMeasure> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UnitsOfMeasure
            .Where(u => !u.IsDeleted)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.UnitsOfMeasure
            .AnyAsync(u => !u.IsDeleted && u.Code == code, cancellationToken);
    }

    public async Task<UnitOfMeasureTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.UnitOfMeasureTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(UnitOfMeasure unitOfMeasure)
    {
        _context.UnitsOfMeasure.Add(unitOfMeasure);
    }

    public void Update(UnitOfMeasure unitOfMeasure)
    {
        _context.UnitsOfMeasure.Update(unitOfMeasure);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
