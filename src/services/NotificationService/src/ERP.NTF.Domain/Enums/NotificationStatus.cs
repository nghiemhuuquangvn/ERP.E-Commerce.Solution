namespace ERP.NTF.Domain.Enums;

public enum NotificationStatus
{
    Queued = 0,
    Sending = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Bounced = 5,
    Cancelled = 6
}
