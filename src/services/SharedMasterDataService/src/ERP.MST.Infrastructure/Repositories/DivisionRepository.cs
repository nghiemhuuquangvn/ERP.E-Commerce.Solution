using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class DivisionRepository : IDivisionRepository
{
    private readonly MasterDataDbContext _context;

    public DivisionRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Division?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Divisions
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Division> Items, int TotalCount)> ListAsync(
        Guid? branchId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Divisions
            .Where(d => !d.IsDeleted)
            .AsQueryable();

        if (branchId.HasValue)
            query = query.Where(d => d.BranchId == branchId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Divisions
            .AnyAsync(d => !d.IsDeleted && d.Code == code, cancellationToken);
    }

    public void Add(Division division)
    {
        _context.Divisions.Add(division);
    }

    public void Update(Division division)
    {
        _context.Divisions.Update(division);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
