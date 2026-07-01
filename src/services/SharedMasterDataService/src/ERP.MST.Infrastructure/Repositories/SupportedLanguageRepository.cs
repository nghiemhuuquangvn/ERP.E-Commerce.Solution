using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class SupportedLanguageRepository : ISupportedLanguageRepository
{
    private readonly MasterDataDbContext _context;

    public SupportedLanguageRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<SupportedLanguage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SupportedLanguages
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<SupportedLanguage> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SupportedLanguages
            .Where(l => !l.IsDeleted)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(l => l.SortOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.SupportedLanguages
            .AnyAsync(l => !l.IsDeleted && l.Code == code, cancellationToken);
    }

    public void Add(SupportedLanguage language)
    {
        _context.SupportedLanguages.Add(language);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
