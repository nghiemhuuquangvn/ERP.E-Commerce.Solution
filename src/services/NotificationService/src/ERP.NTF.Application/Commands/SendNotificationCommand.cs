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

    /// <summary>Set by event handlers: the same key is sent once (a redelivered event is a no-op).</summary>
    public string? DedupKey { get; set; }

    /// <summary>Event handlers: a recipient who turned the channel off is skipped quietly instead of failing the message (and being retried).</summary>
    public bool SkipWhenOptedOut { get; set; }
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

/// <summary>Changes the wording of a template (code and channel never change).</summary>
public class UpdateTemplateCommand : IRequest<NotificationTemplateDto>
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Variables { get; set; }
}

public class SetTemplateActiveCommand : IRequest<NotificationTemplateDto>
{
    public Guid TemplateId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Deletes a shop's own template (the platform default is used again). A platform template cannot be deleted.</summary>
public class DeleteTemplateCommand : IRequest<bool>
{
    public Guid TemplateId { get; set; }
}

/// <summary>A shop copies a platform default into a template of its own (same code), to change it for itself.</summary>
public class OverrideTemplateCommand : IRequest<NotificationTemplateDto>
{
    public Guid TemplateId { get; set; }
}

/// <summary>Renders a text with sample values and lists the placeholders no sample covers. Changes nothing.</summary>
public class PreviewTemplateCommand : IRequest<TemplatePreviewResult>
{
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
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
