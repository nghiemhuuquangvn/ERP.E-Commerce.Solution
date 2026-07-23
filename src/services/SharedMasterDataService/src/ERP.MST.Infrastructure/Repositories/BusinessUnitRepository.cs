using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class BusinessUnitRepository : IBusinessUnitRepository
{
    private readonly MasterDataDbContext _context;

    public BusinessUnitRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<BusinessUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BusinessUnits
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<BusinessUnit> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BusinessUnits
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
        return await _context.BusinessUnits
            .AnyAsync(b => !b.IsDeleted && b.Code == code, cancellationToken);
    }

    public void Add(BusinessUnit businessUnit)
    {
        _context.BusinessUnits.Add(businessUnit);
    }

    public void Update(BusinessUnit businessUnit)
    {
        _context.BusinessUnits.Update(businessUnit);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
