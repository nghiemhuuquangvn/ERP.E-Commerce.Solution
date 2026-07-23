using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class OrganizationCalendarRepository : IOrganizationCalendarRepository
{
    private readonly MasterDataDbContext _context;

    public OrganizationCalendarRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationCalendar?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OrganizationCalendars
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<OrganizationCalendar> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OrganizationCalendars
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(c => c.CompanyId == companyId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Year)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(OrganizationCalendar calendar)
    {
        _context.OrganizationCalendars.Add(calendar);
    }

    public void Update(OrganizationCalendar calendar)
    {
        _context.OrganizationCalendars.Update(calendar);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
