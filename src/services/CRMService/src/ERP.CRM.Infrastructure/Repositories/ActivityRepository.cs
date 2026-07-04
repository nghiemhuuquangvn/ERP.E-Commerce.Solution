using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.CRM.Infrastructure.Repositories;

public class ActivityRepository : IActivityRepository
{
    private readonly CrmDbContext _db;

    public ActivityRepository(CrmDbContext db) => _db = db;

    public async Task<Activity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Activities.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        await _db.Activities.AddAsync(activity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        _db.Activities.Update(activity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Activity> Items, int TotalCount)> GetPagedAsync(
        Guid? contactId,
        Guid? opportunityId,
        int? activityType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Activities.AsQueryable();

        if (contactId.HasValue)
            query = query.Where(a => a.ContactId == contactId.Value);

        if (opportunityId.HasValue)
            query = query.Where(a => a.OpportunityId == opportunityId.Value);

        if (activityType.HasValue)
            query = query.Where(a => a.ActivityType == (Domain.Enums.ActivityType)activityType.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
