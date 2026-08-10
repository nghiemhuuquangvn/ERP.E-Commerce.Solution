using ERP.NTF.Application.DTOs;
using ERP.NTF.Domain.Enums;
using MediatR;

namespace ERP.NTF.Application.Commands;

public class SendNotificationCommand : IRequest<NotificationDto>
{
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string? RecipientDeviceToken { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string? TemplateCode { get; set; }
    public Dictionary<string, string>? TemplateVariables { get; set; }
    public DateTime? ScheduledFor { get; set; }
}

public class CreateTemplateCommand : IRequest<NotificationTemplateDto>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Variables { get; set; }
}

public class UpdatePreferenceCommand : IRequest<NotificationPreferenceDto>
{
    public Guid UserId { get; set; }
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool InAppEnabled { get; set; }
}

public class UnsubscribeCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
}

public class BulkSendNotificationCommand : IRequest<BulkSendResult>
{
    public List<BulkRecipient> Recipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string? TemplateCode { get; set; }
    public Dictionary<string, string>? SharedVariables { get; set; }
    public DateTime? ScheduledFor { get; set; }
}

public class MarkNotificationReadCommand : IRequest<bool>
{
    public Guid NotificationId { get; set; }
}

public class BulkRecipient
{
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string? RecipientDeviceToken { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
}
