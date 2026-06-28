using ERP.NTF.Application.Interfaces;
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
            await ProcessPendingAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var sms = scope.ServiceProvider.GetRequiredService<ISmsSender>();
        var push = scope.ServiceProvider.GetRequiredService<IPushSender>();

        var pending = await notifications.GetPendingAsync(ct);

        foreach (var notification in pending)
        {
            notification.MarkAsSending();
            try
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
                notification.MarkAsSent();
                _logger.LogInformation("Notification {Id} sent via {Channel}", notification.Id, notification.Channel);
            }
            catch (Exception ex)
            {
                notification.MarkAsFailed(ex.Message);
                _logger.LogError(ex, "Failed to send notification {Id}", notification.Id);
            }

            await notifications.UpdateAsync(notification, ct);
        }
    }
}
