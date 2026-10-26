using ERP.Shared.Core.Domain;
using ERP.NTF.Domain.Enums;
using ERP.NTF.Domain.Events;

namespace ERP.NTF.Domain.Entities;

public class Notification : AggregateRoot
{
    private readonly List<NotificationLog> _logs = new();

    public string RecipientId { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string? RecipientPhone { get; private set; }
    public string? RecipientDeviceToken { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public DateTime? ScheduledFor { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    /// <summary>One notification per business fact ("OrderShipped:orderId"): a redelivered event sends nothing twice. Unique per tenant when set.</summary>
    public string? DedupKey { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public IReadOnlyCollection<NotificationLog> Logs => _logs.AsReadOnly();

    private Notification() { }

    public static Notification Create(
        string recipientId,
        string recipientEmail,
        NotificationChannel channel,
        string subject,
        string body,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? templateId = null,
        string? recipientPhone = null,
        string? recipientDeviceToken = null,
        DateTime? scheduledFor = null,
        string? dedupKey = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            RecipientEmail = recipientEmail,
            RecipientPhone = recipientPhone,
            RecipientDeviceToken = recipientDeviceToken,
            Channel = channel,
            TemplateId = templateId,
            Subject = subject,
            Body = body,
            Priority = priority,
            Status = scheduledFor.HasValue ? NotificationStatus.Queued : NotificationStatus.Queued,
            ScheduledFor = scheduledFor,
            DedupKey = dedupKey,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        notification._logs.Add(NotificationLog.Create(notification.Id, "Created", $"Notification queued for {channel}"));
        return notification;
    }

    public void MarkAsSending()
    {
        Status = NotificationStatus.Sending;
        _logs.Add(NotificationLog.Create(Id, "Sending", "Delivery attempt started"));
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        _logs.Add(NotificationLog.Create(Id, "Sent", "Message sent successfully"));
        AddDomainEvent(new NotificationSentEvent(Id, RecipientId, Channel));
    }

    public void MarkAsDelivered()
    {
        Status = NotificationStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        _logs.Add(NotificationLog.Create(Id, "Delivered", "Delivery confirmed"));
    }

    public void MarkAsFailed(string error)
    {
        Status = NotificationStatus.Failed;
        FailedAt = DateTime.UtcNow;
        ErrorMessage = error;
        RetryCount++;
        _logs.Add(NotificationLog.Create(Id, "Failed", $"Delivery failed: {error}"));
        AddDomainEvent(new NotificationFailedEvent(Id, RecipientId, Channel, error));
    }

    public void MarkAsBounced(string reason)
    {
        Status = NotificationStatus.Bounced;
        ErrorMessage = reason;
        _logs.Add(NotificationLog.Create(Id, "Bounced", $"Message bounced: {reason}"));
    }

    public bool CanRetry() => RetryCount < 3 && Status == NotificationStatus.Failed;

    // Exponential backoff: 30s after 1st fail, 60s after 2nd. RetryCount already incremented by MarkAsFailed.
    public bool ShouldRetryNow() =>
        CanRetry() &&
        FailedAt.HasValue &&
        FailedAt.Value.AddSeconds(30 * Math.Pow(2, RetryCount - 1)) <= DateTime.UtcNow;

    public bool IsReadyToSend() =>
        Status == NotificationStatus.Queued &&
        (!ScheduledFor.HasValue || ScheduledFor.Value <= DateTime.UtcNow);

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
        _logs.Add(NotificationLog.Create(Id, "Read", "Marked as read by recipient"));
    }
}
