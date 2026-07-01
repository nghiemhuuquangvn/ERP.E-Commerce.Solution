using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly MasterDataDbContext _context;

    public PositionRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Positions
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Position> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Positions
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Positions
            .AnyAsync(p => !p.IsDeleted && p.Code == code, cancellationToken);
    }

    public async Task<PositionTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.PositionTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(Position position)
    {
        _context.Positions.Add(position);
    }

    public void Update(Position position)
    {
        _context.Positions.Update(position);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
