using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;
using MediatR;

namespace ERP.NTF.Application.Handlers;

public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, NotificationDto>
{
    private readonly INotificationRepository _notifications;
    private readonly ITemplateRepository _templates;
    private readonly IPreferenceRepository _preferences;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IPushSender _push;

    public SendNotificationCommandHandler(
        INotificationRepository notifications,
        ITemplateRepository templates,
        IPreferenceRepository preferences,
        IEmailSender email,
        ISmsSender sms,
        IPushSender push)
    {
        _notifications = notifications;
        _templates = templates;
        _preferences = preferences;
        _email = email;
        _sms = sms;
        _push = push;
    }

    public async Task<NotificationDto> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var preference = await _preferences.GetByUserIdAsync(Guid.Parse(request.RecipientId), cancellationToken);
        if (preference != null && !preference.IsChannelEnabled(request.Channel))
            throw new InvalidOperationException($"Recipient has disabled {request.Channel} notifications.");

        var subject = request.Subject;
        var body = request.Body;

        if (!string.IsNullOrEmpty(request.TemplateCode))
        {
            var template = await _templates.GetByCodeAsync(request.TemplateCode, cancellationToken)
                ?? throw new InvalidOperationException($"Template '{request.TemplateCode}' not found.");

            subject = template.Subject;
            body = template.Render(request.TemplateVariables ?? new());
        }

        var notification = Notification.Create(
            request.RecipientId,
            request.RecipientEmail,
            request.Channel,
            subject,
            body,
            request.Priority,
            null,
            request.RecipientPhone,
            request.RecipientDeviceToken,
            request.ScheduledFor);

        await _notifications.AddAsync(notification, cancellationToken);

        if (request.Priority == NotificationPriority.Critical || request.Priority == NotificationPriority.High)
            await DeliverAsync(notification, cancellationToken);

        return MapToDto(notification);
    }

    private async Task DeliverAsync(Notification notification, CancellationToken ct)
    {
        notification.MarkAsSending();
        try
        {
            switch (notification.Channel)
            {
                case NotificationChannel.Email:
                    await _email.SendAsync(notification.RecipientEmail, notification.Subject, notification.Body, ct);
                    break;
                case NotificationChannel.Sms when notification.RecipientPhone != null:
                    await _sms.SendAsync(notification.RecipientPhone, notification.Body, ct);
                    break;
                case NotificationChannel.Push when notification.RecipientDeviceToken != null:
                    await _push.SendAsync(notification.RecipientDeviceToken, notification.Subject, notification.Body, ct);
                    break;
            }
            notification.MarkAsSent();
        }
        catch (Exception ex)
        {
            notification.MarkAsFailed(ex.Message);
        }
        await _notifications.UpdateAsync(notification, ct);
    }

    private static NotificationDto MapToDto(Notification n) => new()
    {
        Id = n.Id,
        RecipientId = n.RecipientId,
        RecipientEmail = n.RecipientEmail,
        Channel = n.Channel.ToString(),
        Subject = n.Subject,
        Body = n.Body,
        Status = n.Status.ToString(),
        Priority = n.Priority.ToString(),
        RetryCount = n.RetryCount,
        ErrorMessage = n.ErrorMessage,
        ScheduledFor = n.ScheduledFor,
        SentAt = n.SentAt,
        DeliveredAt = n.DeliveredAt,
        CreatedAt = n.CreatedAt,
        Logs = n.Logs.Select(l => new NotificationLogDto
        {
            EventType = l.EventType,
            Details = l.Details,
            EventTimestamp = l.EventTimestamp
        }).ToList()
    };
}
