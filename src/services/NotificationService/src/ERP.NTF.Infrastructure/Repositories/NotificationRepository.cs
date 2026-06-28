using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;
using ERP.NTF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.NTF.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;
    public NotificationRepository(NotificationDbContext db) => _db = db;

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Notifications.Include(n => n.Logs).FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<List<Notification>> GetByRecipientAsync(string recipientId, int skip = 0, int take = 20, CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip).Take(take)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<List<Notification>> GetPendingAsync(CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.Status == NotificationStatus.Queued &&
                        (!n.ScheduledFor.HasValue || n.ScheduledFor.Value <= DateTime.UtcNow))
            .OrderBy(n => n.Priority)
            .ToListAsync(ct);

    public async Task<List<Notification>> GetFailedForRetryAsync(CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < 3)
            .ToListAsync(ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _db.Notifications.AddAsync(notification, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetCountByRecipientAsync(string recipientId, CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => n.RecipientId == recipientId, ct);
}

public class TemplateRepository : ITemplateRepository
{
    private readonly NotificationDbContext _db;
    public TemplateRepository(NotificationDbContext db) => _db = db;

    public async Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Templates.FindAsync(new object[] { id }, ct);

    public async Task<NotificationTemplate?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await _db.Templates.FirstOrDefaultAsync(t => t.Code == code, ct);

    public async Task<List<NotificationTemplate>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var query = _db.Templates.AsQueryable();
        if (isActive.HasValue) query = query.Where(t => t.IsActive == isActive.Value);
        return await query.OrderBy(t => t.Name).AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        await _db.Templates.AddAsync(template, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        _db.Templates.Update(template);
        await _db.SaveChangesAsync(ct);
    }
}

public class PreferenceRepository : IPreferenceRepository
{
    private readonly NotificationDbContext _db;
    public PreferenceRepository(NotificationDbContext db) => _db = db;

    public async Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Preferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task AddAsync(NotificationPreference preference, CancellationToken ct = default)
    {
        await _db.Preferences.AddAsync(preference, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(NotificationPreference preference, CancellationToken ct = default)
    {
        _db.Preferences.Update(preference);
        await _db.SaveChangesAsync(ct);
    }
}
