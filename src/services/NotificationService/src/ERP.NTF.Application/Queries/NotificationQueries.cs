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
