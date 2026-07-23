using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class HolidayRepository : IHolidayRepository
{
    private readonly MasterDataDbContext _context;

    public HolidayRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Holiday?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Holidays
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Holiday> Items, int TotalCount)> ListAsync(
        Guid calendarId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Holidays
            .Where(h => !h.IsDeleted && h.CalendarId == calendarId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(h => h.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(Holiday holiday)
    {
        _context.Holidays.Add(holiday);
    }

    public void Update(Holiday holiday)
    {
        _context.Holidays.Update(holiday);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
