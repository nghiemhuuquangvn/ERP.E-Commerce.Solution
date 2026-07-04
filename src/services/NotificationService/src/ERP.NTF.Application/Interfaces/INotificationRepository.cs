using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Notification>> GetByRecipientAsync(string recipientId, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<List<Notification>> GetPendingAsync(CancellationToken ct = default);
    Task<List<Notification>> GetFailedForRetryAsync(CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task<int> GetCountByRecipientAsync(string recipientId, CancellationToken ct = default);
}

public interface IWebhookRepository
{
    Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<WebhookEndpoint>> GetAllAsync(CancellationToken ct = default);
    Task<List<WebhookEndpoint>> GetActiveByEventTypeAsync(string eventType, CancellationToken ct = default);
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<NotificationTemplate>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default);
}

public interface IPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(NotificationPreference preference, CancellationToken ct = default);
    Task UpdateAsync(NotificationPreference preference, CancellationToken ct = default);
}
