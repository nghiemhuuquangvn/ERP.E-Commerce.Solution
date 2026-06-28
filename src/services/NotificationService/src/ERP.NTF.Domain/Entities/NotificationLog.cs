using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities;

public class NotificationLog : BaseEntity
{
    public Guid NotificationId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public DateTime EventTimestamp { get; private set; }

    private NotificationLog() { }

    public static NotificationLog Create(Guid notificationId, string eventType, string details)
    {
        return new NotificationLog
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            EventType = eventType,
            Details = details,
            EventTimestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
