using ERP.Shared.Infrastructure.Data;
using Confluent.Kafka;
using ERP.CRM.Application.EventHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.CRM.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Consumes IdentityService's "identity-topic" and PaymentService's "payment-topic" —
    /// both publish via the Outbox pattern with Newtonsoft TypeNameHandling.All, so the
    /// "$type" discriminator holds the publisher's fully-qualified CLR type name. Dispatch
    /// is by substring match against that, mirroring HRMService's MasterDataEventConsumer /
    /// NotificationService's IdentityEventConsumer pattern.
    ///
    /// Unlike those two services, CRM's existing event handlers (UserCreatedEventHandler,
    /// PaymentProcessedEventHandler) implement ERP.Shared.Messaging.IEventHandler&lt;T&gt;,
    /// not MediatR's INotificationHandler&lt;T&gt; — so instead of IMediator.Publish this
    /// resolves the concrete handler from a DI scope and calls HandleAsync directly, the
    /// same invocation style HRMService's MasterDataEventConsumer already uses for its own
    /// (non-MediatR) handler class. This reuses the existing handler-invocation mechanism
    /// rather than introducing a parallel one.
    /// </summary>
    public class CrmKafkaConsumer : IHostedService
    {
        private const string IdentityTopic = "identity-topic";
        private const string PaymentTopic = "payment-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CrmKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public CrmKafkaConsumer(
            IServiceProvider serviceProvider,
            ILogger<CrmKafkaConsumer> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "crm-service-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(new[] { IdentityTopic, PaymentTopic });
            _executingTask = Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null) return;

            _cancellationTokenSource?.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));

            _consumer.Close();
            _consumer.Dispose();
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromSeconds(5));
                    if (result?.Message?.Value == null)
                        continue;

                    await DispatchAsync(result.Message.Value, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming from identity-topic/payment-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from identity-topic/payment-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

            using var scope = _serviceProvider.CreateScope();

            if (typeName.Contains("UserRegisteredDomainEvent"))
            {
                // IdentityService's UserRegisteredDomainEvent carries
                // (UserId, TenantId, Email, UserType, OccurredOn) — no display name —
                // so we fall back to Email for the Contact's required Name field,
                // matching NotificationService's IdentityEventConsumer precedent of
                // filling unavailable fields with a safe default rather than guessing.
                var raw = JObject.Parse(json);
                var userId = raw.Value<Guid?>("UserId") ?? Guid.Empty;
                var tenantId = raw.Value<Guid?>("TenantId") ?? Guid.Empty;
                var email = raw.Value<string>("Email") ?? string.Empty;

                if (userId == Guid.Empty)
                    return;
                if (tenantId == Guid.Empty)
                {
                    _logger.LogWarning("UserRegisteredDomainEvent for UserId={UserId} has no TenantId — Contact not created", userId);
                    return;
                }

                // No HTTP user here: the Contact belongs to the tenant the user registered in.
                using var tenantScope = TenantScope.Begin(tenantId);
                var evt = new UserRegisteredIntegrationEvent(userId, email, email);
                var handler = scope.ServiceProvider.GetRequiredService<UserCreatedEventHandler>();
                await handler.HandleAsync(evt, cancellationToken);
            }
            else if (typeName.Contains("PaymentProcessedEvent"))
            {
                // PaymentService's PaymentProcessedEvent field names (PaymentId, OrderId,
                // Amount, ...) map 1:1 onto the local contract, so a direct deserialize is
                // sufficient here (no manual field-by-field mapping needed).
                var evt = JsonConvert.DeserializeObject<PaymentProcessedIntegrationEvent>(json);
                if (evt != null)
                {
                    var handler = scope.ServiceProvider.GetRequiredService<PaymentProcessedEventHandler>();
                    await handler.HandleAsync(evt, cancellationToken);
                }
            }
            // Other identity-topic/payment-topic events are not yet consumed by CRM — no
            // local handler exists for them today.
        }
    }
}
