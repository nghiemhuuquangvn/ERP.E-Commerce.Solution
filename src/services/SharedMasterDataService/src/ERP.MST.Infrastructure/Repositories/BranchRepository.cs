using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly MasterDataDbContext _context;

    public BranchRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Branch> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Branches
            .Where(b => !b.IsDeleted)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(b => b.CompanyId == companyId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .AnyAsync(b => !b.IsDeleted && b.Code == code, cancellationToken);
    }

    public void Add(Branch branch)
    {
        _context.Branches.Add(branch);
    }

    public void Update(Branch branch)
    {
        _context.Branches.Update(branch);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
