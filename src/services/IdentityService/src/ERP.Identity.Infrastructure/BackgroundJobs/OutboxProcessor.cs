using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.Identity.Infrastructure.BackgroundJobs
{
    public class OutboxProcessor : BackgroundService
    {
        // Never webhook-eligible regardless of a subscriber's EventTypes filter — this
        // domain event carries a plaintext 2FA code (TwoFactorCodeGeneratedDomainEvent.Code)
        // that must never reach an external URL.
        private static readonly HashSet<string> WebhookExcludedEventTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "TwoFactorCodeGeneratedDomainEvent",
            // Carry single-use reset/confirmation links (plaintext tokens) — only
            // NotificationService may see these, never a tenant-configured webhook URL.
            "PasswordResetRequestedDomainEvent",
            "EmailConfirmationRequestedDomainEvent",
        };

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(ILogger<OutboxProcessor> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox Processor started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // TẠO SCOPE Ở ĐÂY
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // Lấy Repository và MessageBus từ Scope
                        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var webhookSender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();

                        // 1. Lấy tin nhắn chưa xử lý
                        var messages = await unitOfWork.OutboxMessages.GetPendingMessagesAsync(stoppingToken);
                        if (!messages.Any())
                        {
                            await Task.Delay(5000, stoppingToken);
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            try
                            {
                                // Gửi lên Kafka
                                await messageBus.PublishAsync("identity-topic", message.Content, stoppingToken);

                                message.ProcessedOnUtc = DateTime.UtcNow;
                                message.Error = null; // Xóa lỗi cũ nếu có

                                // Delivered: the code / link has done its job, so it must not stay in the table.
                                message.Content = OutboxPayloadProtector.Redact(message.Type, message.Content);

                                // Fire-and-forget webhook delivery alongside the Kafka publish —
                                // mirrors NotificationService's WebhookSender pattern exactly (no
                                // persisted retry queue). A webhook failure never affects the
                                // OutboxMessage's own processed/error state — Kafka delivery is
                                // the durable guarantee, webhooks are best-effort.
                                await DeliverWebhooksAsync(unitOfWork, webhookSender, message.Type, message.Content, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to publish message {Id}", message.Id);
                                message.Error = ex.Message;
                                // Không throw ở đây để tiếp tục xử lý các message khác trong lô
                            }
                        }

                        // 2. Lưu lại trạng thái của cả lô
                        // Vì chúng ta đang dùng DbContext từ Scope, SaveChanges ở đây
                        // chỉ cập nhật trạng thái "Đã xử lý" cho bảng Outbox.
                        await unitOfWork.SaveChangesAsync(stoppingToken);
                    }
                    // Khi kết thúc khối using, Scope sẽ bị hủy, Repository và DbContext sẽ được giải phóng sạch sẽ.
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Host is shutting down (pod restart/rollout) — Task.Delay above throws this
                    // as part of normal graceful shutdown, not an actual processing failure.
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý Outbox");
                }

                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Matches a just-published domain event against this tenant's active
        /// WebhookEndpoint subscriptions and fires each one. TenantId is read out of the
        /// event's own serialized JSON (every domain event in this service carries it —
        /// see UserRegisteredDomainEvent/PermissionGrantedDomainEvent/etc.) rather than a
        /// dedicated OutboxMessage column, since that table has no tenant concept today.
        /// </summary>
        private async Task DeliverWebhooksAsync(
            IUnitOfWork unitOfWork, IWebhookSender webhookSender, string? eventType, string? content, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(content))
                return;

            if (WebhookExcludedEventTypes.Contains(eventType))
                return;

            Guid tenantId;
            try
            {
                var parsed = JObject.Parse(content)["TenantId"]?.ToString();
                if (!Guid.TryParse(parsed, out tenantId))
                    return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse TenantId out of outbox event {EventType} for webhook matching", eventType);
                return;
            }

            var endpoints = await unitOfWork.WebhookEndpoints.ListActiveByTenantAsync(tenantId, ct);
            foreach (var endpoint in endpoints)
            {
                if (!endpoint.Matches(eventType))
                    continue;

                await webhookSender.SendAsync(endpoint.Url, endpoint.Secret, eventType, new { eventType, tenantId, data = JObject.Parse(content) }, ct);
            }
        }
    }
}