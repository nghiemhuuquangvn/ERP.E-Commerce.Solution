using ERP.NTF.Domain.Enums;
using ERP.Shared.Core.Domain;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Queries;
using MediatR;

namespace ERP.NTF.Application.Handlers;

public class GetNotificationQueryHandler : IRequestHandler<GetNotificationQuery, NotificationDto?>
{
    private readonly INotificationRepository _notifications;

    public GetNotificationQueryHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<NotificationDto?> Handle(GetNotificationQuery request, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification == null) return null;

        return new NotificationDto
        {
            Id = notification.Id,
            RecipientId = notification.RecipientId,
            RecipientEmail = notification.RecipientEmail,
            Channel = notification.Channel.ToString(),
            Subject = notification.Subject,
            Body = notification.Body,
            Status = notification.Status.ToString(),
            Priority = notification.Priority.ToString(),
            RetryCount = notification.RetryCount,
            ErrorMessage = notification.ErrorMessage,
            ScheduledFor = notification.ScheduledFor,
            SentAt = notification.SentAt,
            DeliveredAt = notification.DeliveredAt,
            CreatedAt = notification.CreatedAt,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            Logs = notification.Logs.Select(l => new NotificationLogDto
            {
                EventType = l.EventType,
                Details = l.Details,
                EventTimestamp = l.EventTimestamp
            }).ToList()
        };
    }
}

public class GetNotificationHistoryQueryHandler : IRequestHandler<GetNotificationHistoryQuery, NotificationHistoryResponse>
{
    private readonly INotificationRepository _notifications;

    public GetNotificationHistoryQueryHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<NotificationHistoryResponse> Handle(GetNotificationHistoryQuery request, CancellationToken cancellationToken)
    {
        var items = await _notifications.GetByRecipientAsync(request.RecipientId, request.Skip, request.Take, cancellationToken);
        var total = await _notifications.GetCountByRecipientAsync(request.RecipientId, cancellationToken);

        return new NotificationHistoryResponse
        {
            Total = total,
            Items = items.Select(n => new NotificationDto
            {
                Id = n.Id,
                RecipientId = n.RecipientId,
                RecipientEmail = n.RecipientEmail,
                Channel = n.Channel.ToString(),
                Subject = n.Subject,
                Status = n.Status.ToString(),
                Priority = n.Priority.ToString(),
                SentAt = n.SentAt,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt
            }).ToList()
        };
    }
}

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationRepository _notifications;

    public GetUnreadCountQueryHandler(INotificationRepository notifications) => _notifications = notifications;

    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken) =>
        _notifications.GetUnreadCountByRecipientAsync(request.RecipientId, cancellationToken);
}

public class ListNotificationsQueryHandler : IRequestHandler<ListNotificationsQuery, NotificationHistoryResponse>
{
    private readonly INotificationRepository _notifications;
    public ListNotificationsQueryHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<NotificationHistoryResponse> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        NotificationStatus? status = Enum.TryParse<NotificationStatus>(request.Status, true, out var s) ? s : null;
        NotificationChannel? channel = Enum.TryParse<NotificationChannel>(request.Channel, true, out var c) ? c : null;
        var take = Math.Clamp(request.Take, 1, 100);

        var (items, total) = await _notifications.ListAsync(status, channel, request.Search, Math.Max(0, request.Skip), take, cancellationToken);
        return new NotificationHistoryResponse
        {
            Total = total,
            Items = items.Select(SendNotificationCommandHandler.MapToDto).ToList()
        };
    }
}

public class GetTemplateQueryHandler : IRequestHandler<GetTemplateQuery, NotificationTemplateDto?>
{
    private readonly ITemplateRepository _templates;

    public GetTemplateQueryHandler(ITemplateRepository templates) => _templates = templates;

    public async Task<NotificationTemplateDto?> Handle(GetTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = request.TemplateId.HasValue
            ? await _templates.GetByIdAsync(request.TemplateId.Value, cancellationToken)
            : request.Code != null ? await _templates.GetByCodeAsync(request.Code, cancellationToken) : null;

        if (template == null) return null;

        return new NotificationTemplateDto
        {
            Id = template.Id,
            Code = template.Code,
            Name = template.Name,
            Channel = template.Channel.ToString(),
            Subject = template.Subject,
            Body = template.Body,
            Variables = template.Variables,
            IsActive = template.IsActive
        };
    }
}

public class ListTemplatesQueryHandler : IRequestHandler<ListTemplatesQuery, List<NotificationTemplateDto>>
{
    private readonly ITemplateRepository _templates;

    public ListTemplatesQueryHandler(ITemplateRepository templates) => _templates = templates;

    public async Task<List<NotificationTemplateDto>> Handle(ListTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _templates.GetAllAsync(request.IsActive, cancellationToken);

        // A tenant's own template replaces the platform default with the same code (as when a message is sent): one row per code.
        templates = templates
            .GroupBy(t => t.Code)
            .Select(g => g.OrderBy(t => t.TenantId == WellKnownTenants.Platform ? 1 : 0).First())
            .OrderBy(t => t.Name)
            .ToList();

        return templates.Select(t => new NotificationTemplateDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Channel = t.Channel.ToString(),
            Subject = t.Subject,
            Body = t.Body,
            Variables = t.Variables,
            IsActive = t.IsActive,
            IsPlatformDefault = t.TenantId == WellKnownTenants.Platform
        }).ToList();
    }
}

public class GetPreferenceQueryHandler : IRequestHandler<GetPreferenceQuery, NotificationPreferenceDto?>
{
    private readonly IPreferenceRepository _preferences;

    public GetPreferenceQueryHandler(IPreferenceRepository preferences) => _preferences = preferences;

    public async Task<NotificationPreferenceDto?> Handle(GetPreferenceQuery request, CancellationToken cancellationToken)
    {
        var pref = await _preferences.GetByUserIdAsync(request.UserId, cancellationToken);
        if (pref == null) return null;

        return new NotificationPreferenceDto
        {
            UserId = pref.UserId,
            EmailEnabled = pref.EmailEnabled,
            SmsEnabled = pref.SmsEnabled,
            PushEnabled = pref.PushEnabled,
            InAppEnabled = pref.InAppEnabled
        };
    }
}
