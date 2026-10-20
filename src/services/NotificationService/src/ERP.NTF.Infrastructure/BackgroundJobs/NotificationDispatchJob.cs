using ERP.Shared.Infrastructure.Data;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Infrastructure.BackgroundJobs;

public class NotificationDispatchJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatchJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    public NotificationDispatchJob(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatchJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // No user here: find tenants with work across tenants, then send each tenant's
            // notifications inside its TenantScope (so its own templates/channel settings apply).
            List<Guid> tenants;
            using (var lookup = _scopeFactory.CreateScope())
                tenants = await lookup.ServiceProvider.GetRequiredService<INotificationRepository>()
                    .GetTenantsWithWorkAsync(stoppingToken);

            foreach (var tenantId in tenants.Where(t => t != Guid.Empty))
            {
                using (TenantScope.Begin(tenantId))
                {
                    await ProcessPendingAsync(stoppingToken);
                    await ProcessRetriesAsync(stoppingToken);
                }
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var (notifications, email, sms, push, webhooks, webhookSender) = ResolveServices(scope);

        var pending = await notifications.GetPendingAsync(ct);
        foreach (var notification in pending)
        {
            notification.MarkAsSending();
            try
            {
                await SendViaChannelAsync(notification, email, sms, push, ct);
                notification.MarkAsSent();
                _logger.LogInformation("Notification {Id} sent via {Channel}", notification.Id, notification.Channel);
                await FireWebhooksAsync(webhooks, webhookSender, "notification.sent", notification, ct);
            }
            catch (Exception ex)
            {
                notification.MarkAsFailed(ex.Message);
                _logger.LogError(ex, "Failed to send notification {Id}", notification.Id);
            }
            await notifications.UpdateAsync(notification, ct);
        }
    }

    private async Task ProcessRetriesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var (notifications, email, sms, push, webhooks, webhookSender) = ResolveServices(scope);

        var failedForRetry = await notifications.GetFailedForRetryAsync(ct);
        foreach (var notification in failedForRetry.Where(n => n.ShouldRetryNow()))
        {
            notification.MarkAsSending();
            try
            {
                await SendViaChannelAsync(notification, email, sms, push, ct);
                notification.MarkAsSent();
                _logger.LogInformation("Notification {Id} resent on retry {Count} via {Channel}",
                    notification.Id, notification.RetryCount, notification.Channel);
                await FireWebhooksAsync(webhooks, webhookSender, "notification.sent", notification, ct);
            }
            catch (Exception ex)
            {
                notification.MarkAsFailed(ex.Message);
                _logger.LogWarning("Retry {Count} failed for notification {Id}: {Error}",
                    notification.RetryCount, notification.Id, ex.Message);
            }
            await notifications.UpdateAsync(notification, ct);
        }
    }

    private static (INotificationRepository, IEmailSender, ISmsSender, IPushSender, IWebhookRepository, IWebhookSender) ResolveServices(IServiceScope scope)
    {
        var sp = scope.ServiceProvider;
        return (
            sp.GetRequiredService<INotificationRepository>(),
            sp.GetRequiredService<IEmailSender>(),
            sp.GetRequiredService<ISmsSender>(),
            sp.GetRequiredService<IPushSender>(),
            sp.GetRequiredService<IWebhookRepository>(),
            sp.GetRequiredService<IWebhookSender>()
        );
    }

    private static async Task SendViaChannelAsync(
        Notification notification,
        IEmailSender email, ISmsSender sms, IPushSender push,
        CancellationToken ct)
    {
        switch (notification.Channel)
        {
            case NotificationChannel.Email:
                await email.SendAsync(notification.RecipientEmail, notification.Subject, notification.Body, ct);
                break;
            case NotificationChannel.Sms when notification.RecipientPhone != null:
                await sms.SendAsync(notification.RecipientPhone, notification.Body, ct);
                break;
            case NotificationChannel.Push when notification.RecipientDeviceToken != null:
                await push.SendAsync(notification.RecipientDeviceToken, notification.Subject, notification.Body, ct);
                break;
        }
    }

    private static async Task FireWebhooksAsync(
        IWebhookRepository webhooks, IWebhookSender sender,
        string eventType, Notification notification,
        CancellationToken ct)
    {
        var endpoints = await webhooks.GetActiveByEventTypeAsync(eventType, ct);
        foreach (var endpoint in endpoints)
        {
            await sender.SendAsync(endpoint.Url, endpoint.Secret, eventType, new
            {
                notification.Id,
                notification.RecipientId,
                notification.RecipientEmail,
                Channel = notification.Channel.ToString(),
                notification.Subject,
                Status = notification.Status.ToString(),
                notification.SentAt
            }, ct);
        }
    }
}
