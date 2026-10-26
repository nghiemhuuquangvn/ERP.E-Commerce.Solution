using ERP.NTF.Application.DTOs;
using MediatR;

namespace ERP.NTF.Application.Queries;

public class GetNotificationQuery : IRequest<NotificationDto?>
{
    public Guid NotificationId { get; set; }
}

public class GetNotificationHistoryQuery : IRequest<NotificationHistoryResponse>
{
    public string RecipientId { get; set; } = string.Empty;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
}

public class NotificationHistoryResponse
{
    public int Total { get; set; }
    public List<NotificationDto> Items { get; set; } = new();
}

/// <summary>Every notification of the tenant, newest first, for the admin history page (ROADMAP P1-6).</summary>
public class ListNotificationsQuery : IRequest<NotificationHistoryResponse>
{
    public string? Status { get; set; }
    public string? Channel { get; set; }
    public string? Search { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
}

public class GetUnreadCountQuery : IRequest<int>
{
    public string RecipientId { get; set; } = string.Empty;
}

public class GetTemplateQuery : IRequest<NotificationTemplateDto?>
{
    public Guid? TemplateId { get; set; }
    public string? Code { get; set; }
}

public class ListTemplatesQuery : IRequest<List<NotificationTemplateDto>>
{
    public bool? IsActive { get; set; }
}

public class GetPreferenceQuery : IRequest<NotificationPreferenceDto?>
{
    public Guid UserId { get; set; }
}

public class ListWebhooksQuery : IRequest<List<WebhookDto>> { }

public class GetWebhookQuery : IRequest<WebhookDto?>
{
    public Guid WebhookId { get; set; }
}
