using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Notification>> GetByRecipientAsync(string recipientId, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<List<Notification>> GetPendingAsync(CancellationToken ct = default);
    Task<List<Notification>> GetFailedForRetryAsync(CancellationToken ct = default);
    /// <summary>Tenants with queued or retryable notifications — across tenants, for the dispatch job.</summary>
    Task<List<Guid>> GetTenantsWithWorkAsync(CancellationToken ct = default);
    Task<(List<Notification> Items, int Total)> ListAsync(NotificationStatus? status, NotificationChannel? channel, string? search, int skip, int take, CancellationToken ct = default);
    Task<Notification?> GetByDedupKeyAsync(string dedupKey, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task<int> GetCountByRecipientAsync(string recipientId, CancellationToken ct = default);
    Task<int> GetUnreadCountByRecipientAsync(string recipientId, CancellationToken ct = default);
}

public interface IWebhookRepository
{
    Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<WebhookEndpoint>> GetAllAsync(CancellationToken ct = default);
    Task<List<WebhookEndpoint>> GetActiveByEventTypeAsync(string eventType, CancellationToken ct = default);
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    /// <summary>Deletes the endpoint and its delivery log. False when there is no such endpoint.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task AddDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task<(List<WebhookDelivery> Items, int Total)> ListDeliveriesAsync(Guid endpointId, int skip, int take, CancellationToken ct = default);
    Task<Dictionary<Guid, WebhookDelivery>> GetLastDeliveriesAsync(IEnumerable<Guid> endpointIds, CancellationToken ct = default);
    /// <summary>Removes the delivery log older than <paramref name="before"/> for the current tenant.</summary>
    Task<int> PurgeDeliveriesAsync(DateTime before, CancellationToken ct = default);
}

public interface ITemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<NotificationTemplate>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default);
    /// <summary>Whether the current tenant has its OWN template with this code (the platform default does not count).</summary>
    Task<bool> ExistsForTenantAsync(string code, Guid tenantId, CancellationToken ct = default);
    /// <summary>True when templates with this code exist but none is active: the message was switched off on purpose.</summary>
    Task<bool> IsSwitchedOffAsync(string code, CancellationToken ct = default);
    Task DeleteAsync(NotificationTemplate template, CancellationToken ct = default);
}

public interface IPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(NotificationPreference preference, CancellationToken ct = default);
    Task UpdateAsync(NotificationPreference preference, CancellationToken ct = default);
}

/// <summary>Local projections of who to notify (ROADMAP P1-6): the customer of an order, and an account's contact.</summary>
public interface IRecipientRepository
{
    Task<OrderRecipient?> GetOrderRecipientAsync(Guid orderId, CancellationToken ct = default);
    Task SaveOrderRecipientAsync(OrderRecipient recipient, CancellationToken ct = default);
    Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken ct = default);
    Task SaveUserContactAsync(UserContact contact, CancellationToken ct = default);
}
