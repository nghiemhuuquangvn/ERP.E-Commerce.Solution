using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class CostCenterRepository : ICostCenterRepository
{
    private readonly MasterDataDbContext _context;

    public CostCenterRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<CostCenter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CostCenters
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<CostCenter> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CostCenters
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(c => c.CompanyId == companyId.Value);

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
        return await _context.CostCenters
            .AnyAsync(c => !c.IsDeleted && c.Code == code, cancellationToken);
    }

    public void Add(CostCenter costCenter)
    {
        _context.CostCenters.Add(costCenter);
    }

    public void Update(CostCenter costCenter)
    {
        _context.CostCenters.Update(costCenter);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
