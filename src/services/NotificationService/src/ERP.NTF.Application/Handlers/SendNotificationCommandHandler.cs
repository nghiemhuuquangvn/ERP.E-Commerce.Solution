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
    private readonly IRealtimeNotifier _realtime;

    public SendNotificationCommandHandler(
        INotificationRepository notifications,
        ITemplateRepository templates,
        IPreferenceRepository preferences,
        IEmailSender email,
        ISmsSender sms,
        IPushSender push,
        IRealtimeNotifier realtime)
    {
        _notifications = notifications;
        _templates = templates;
        _preferences = preferences;
        _email = email;
        _sms = sms;
        _push = push;
        _realtime = realtime;
    }

    public async Task<NotificationDto> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        // A redelivered business event must not send twice: the same key is answered with the notification already made.
        if (!string.IsNullOrEmpty(request.DedupKey))
        {
            var existing = await _notifications.GetByDedupKeyAsync(request.DedupKey, cancellationToken);
            if (existing != null) return MapToDto(existing);
        }

        // RecipientId is a user id for account notifications; for others (a customer id, an order id) there is no preference to read.
        var preference = Guid.TryParse(request.RecipientId, out var recipientUserId)
            ? await _preferences.GetByUserIdAsync(recipientUserId, cancellationToken)
            : null;
        if (preference != null && !preference.IsChannelEnabled(request.Channel))
        {
            if (request.SkipWhenOptedOut)
                return new NotificationDto { RecipientId = request.RecipientId, Channel = request.Channel.ToString(), Status = "Skipped", Subject = request.Subject, Body = request.Body };
            throw new InvalidOperationException($"Recipient has disabled {request.Channel} notifications.");
        }

        var subject = request.Subject;
        var body = request.Body;
        Guid? templateId = null;

        if (!string.IsNullOrEmpty(request.TemplateCode))
        {
            var template = await _templates.GetByCodeAsync(request.TemplateCode, cancellationToken)
                ?? throw new InvalidOperationException($"Template '{request.TemplateCode}' not found.");

            var variables = request.TemplateVariables ?? new();
            subject = template.RenderSubject(variables);
            body = template.Render(variables);
            templateId = template.Id;
        }

        var notification = Notification.Create(
            request.RecipientId,
            request.RecipientEmail,
            request.Channel,
            subject,
            body,
            request.Priority,
            templateId,
            request.RecipientPhone,
            request.RecipientDeviceToken,
            request.ScheduledFor,
            request.DedupKey);

        await _notifications.AddAsync(notification, cancellationToken);

        // InApp always delivers immediately, regardless of priority — a bell
        // notification that waits for a priority flag or the batch dispatch job would
        // defeat the point of a "real-time" bell.
        if (request.Priority == NotificationPriority.Critical || request.Priority == NotificationPriority.High
            || request.Channel == NotificationChannel.InApp)
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
                case NotificationChannel.InApp:
                    await _realtime.NotifyAsync(notification.RecipientId, MapToDto(notification), ct);
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

    internal static NotificationDto MapToDto(Notification n) => new()
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
        IsRead = n.IsRead,
        ReadAt = n.ReadAt,
        Logs = n.Logs.Select(l => new NotificationLogDto
        {
            EventType = l.EventType,
            Details = l.Details,
            EventTimestamp = l.EventTimestamp
        }).ToList()
    };
}

public class BulkSendNotificationCommandHandler : IRequestHandler<BulkSendNotificationCommand, BulkSendResult>
{
    private readonly INotificationRepository _notifications;
    private readonly ITemplateRepository _templates;
    private readonly IPreferenceRepository _preferences;

    public BulkSendNotificationCommandHandler(
        INotificationRepository notifications,
        ITemplateRepository templates,
        IPreferenceRepository preferences)
    {
        _notifications = notifications;
        _templates = templates;
        _preferences = preferences;
    }

    public async Task<BulkSendResult> Handle(BulkSendNotificationCommand request, CancellationToken cancellationToken)
    {
        NotificationTemplate? template = null;
        if (!string.IsNullOrEmpty(request.TemplateCode))
        {
            template = await _templates.GetByCodeAsync(request.TemplateCode, cancellationToken)
                ?? throw new InvalidOperationException($"Template '{request.TemplateCode}' not found.");
        }

        var result = new BulkSendResult();
        var toAdd = new List<Notification>();

        foreach (var recipient in request.Recipients)
        {
            if (Guid.TryParse(recipient.RecipientId, out var userId))
            {
                var preference = await _preferences.GetByUserIdAsync(userId, cancellationToken);
                if (preference != null && !preference.IsChannelEnabled(request.Channel))
                {
                    result.Skipped++;
                    continue;
                }
            }

            var subject = request.Subject;
            var body = request.Body;

            if (template != null)
            {
                var vars = new Dictionary<string, string>(request.SharedVariables ?? new());
                if (recipient.Variables != null)
                    foreach (var kv in recipient.Variables)
                        vars[kv.Key] = kv.Value;
                subject = template.RenderSubject(vars);
                body = template.Render(vars);
            }

            var notification = Notification.Create(
                recipient.RecipientId,
                recipient.RecipientEmail,
                request.Channel,
                subject,
                body,
                request.Priority,
                template?.Id,
                recipient.RecipientPhone,
                recipient.RecipientDeviceToken,
                request.ScheduledFor);

            toAdd.Add(notification);
            result.NotificationIds.Add(notification.Id);
        }

        if (toAdd.Count > 0)
            await _notifications.AddRangeAsync(toAdd, cancellationToken);

        result.Queued = toAdd.Count;
        return result;
    }
}
