using ERP.Shared.Core.Domain;
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

    public async Task<List<Guid>> GetTenantsWithWorkAsync(CancellationToken ct = default) =>
        await _db.Notifications
            .IgnoreQueryFilters() // dispatch job: finds work in every tenant, then sends per tenant
            .Where(n => !n.IsDeleted && (
                (n.Status == NotificationStatus.Queued && (!n.ScheduledFor.HasValue || n.ScheduledFor.Value <= DateTime.UtcNow))
                || (n.Status == NotificationStatus.Failed && n.RetryCount < 3)))
            .Select(n => n.TenantId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<Notification?> GetByDedupKeyAsync(string dedupKey, CancellationToken ct = default) =>
        await _db.Notifications.Include(n => n.Logs).FirstOrDefaultAsync(n => n.DedupKey == dedupKey, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _db.Notifications.AddAsync(notification, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
    {
        await _db.Notifications.AddRangeAsync(notifications, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetCountByRecipientAsync(string recipientId, CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => n.RecipientId == recipientId, ct);

    public async Task<int> GetUnreadCountByRecipientAsync(string recipientId, CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => n.RecipientId == recipientId && !n.IsRead, ct);
}

public class TemplateRepository : ITemplateRepository
{
    private readonly NotificationDbContext _db;
    public TemplateRepository(NotificationDbContext db) => _db = db;

    public async Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Templates.FindAsync(new object[] { id }, ct);

    public async Task<NotificationTemplate?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        // Templates are shared defaults (platform tenant) plus tenant overrides with the same
        // code: the tenant's own template wins.
        await _db.Templates
            .Where(t => t.Code == code)
            .OrderBy(t => t.TenantId == WellKnownTenants.Platform ? 1 : 0)
            .FirstOrDefaultAsync(ct);

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

public class WebhookRepository : IWebhookRepository
{
    private readonly NotificationDbContext _db;
    public WebhookRepository(NotificationDbContext db) => _db = db;

    public async Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.WebhookEndpoints.FindAsync(new object[] { id }, ct);

    public async Task<List<WebhookEndpoint>> GetAllAsync(CancellationToken ct = default) =>
        await _db.WebhookEndpoints.OrderBy(w => w.Name).AsNoTracking().ToListAsync(ct);

    public async Task<List<WebhookEndpoint>> GetActiveByEventTypeAsync(string eventType, CancellationToken ct = default)
    {
        // Load active endpoints in-memory and filter by event type to avoid partial-match SQL issues.
        var active = await _db.WebhookEndpoints.Where(w => w.IsActive).AsNoTracking().ToListAsync(ct);
        return active.Where(w => !w.EventTypes.Any() || w.EventTypes.Contains(eventType)).ToList();
    }

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        await _db.WebhookEndpoints.AddAsync(endpoint, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        _db.WebhookEndpoints.Update(endpoint);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _db.WebhookEndpoints.FindAsync(new object[] { id }, ct);
        if (endpoint != null)
        {
            _db.WebhookEndpoints.Remove(endpoint);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class RecipientRepository : IRecipientRepository
{
    private readonly NotificationDbContext _db;
    public RecipientRepository(NotificationDbContext db) => _db = db;

    public Task<OrderRecipient?> GetOrderRecipientAsync(Guid orderId, CancellationToken ct = default) =>
        _db.OrderRecipients.FirstOrDefaultAsync(r => r.OrderId == orderId && !r.IsDeleted, ct);

    public async Task SaveOrderRecipientAsync(OrderRecipient recipient, CancellationToken ct = default)
    {
        var existing = await _db.OrderRecipients.FirstOrDefaultAsync(r => r.OrderId == recipient.OrderId, ct);
        if (existing == null)
        {
            recipient.Id = Guid.NewGuid();
            await _db.OrderRecipients.AddAsync(recipient, ct);
        }
        else
        {
            existing.CustomerId = recipient.CustomerId;
            existing.OrderNo = recipient.OrderNo;
            existing.CustomerName = recipient.CustomerName;
            existing.Email = recipient.Email;
            existing.Phone = recipient.Phone;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken ct = default) =>
        _db.UserContacts.FirstOrDefaultAsync(c => c.UserId == userId && !c.IsDeleted, ct);

    public async Task SaveUserContactAsync(UserContact contact, CancellationToken ct = default)
    {
        var existing = await _db.UserContacts.FirstOrDefaultAsync(c => c.UserId == contact.UserId, ct);
        if (existing == null)
        {
            contact.Id = Guid.NewGuid();
            await _db.UserContacts.AddAsync(contact, ct);
        }
        else
        {
            existing.Email = contact.Email;
            existing.FullName = contact.FullName;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
