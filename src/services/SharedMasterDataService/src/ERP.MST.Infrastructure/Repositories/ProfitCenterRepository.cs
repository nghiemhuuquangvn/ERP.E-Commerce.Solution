using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class ProfitCenterRepository : IProfitCenterRepository
{
    private readonly MasterDataDbContext _context;

    public ProfitCenterRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<ProfitCenter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProfitCenters
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<ProfitCenter> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ProfitCenters
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(p => p.CompanyId == companyId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.ProfitCenters
            .AnyAsync(p => !p.IsDeleted && p.Code == code, cancellationToken);
    }

    public void Add(ProfitCenter profitCenter)
    {
        _context.ProfitCenters.Add(profitCenter);
    }

    public void Update(ProfitCenter profitCenter)
    {
        _context.ProfitCenters.Update(profitCenter);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
