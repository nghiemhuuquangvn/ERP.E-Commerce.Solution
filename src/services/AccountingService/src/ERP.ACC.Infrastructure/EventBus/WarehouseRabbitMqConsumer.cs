using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.ACC.Application.EventHandlers;

namespace ERP.ACC.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes WarehouseService's "warehouse_exchange" topic exchange (plain System.Text.Json, no "$type", routing key
    /// "warehouse.&lt;EventName&gt;"). Only <c>StockMovementRecordedEvent</c> reaches the ledger: it is one event per stock movement and
    /// carries the cost (ROADMAP P1-1b). Same retry policy as <see cref="SalesOrderRabbitMqConsumer"/>: the handler is idempotent,
    /// a failing message is republished up to <see cref="MaxAttempts"/> times, then dropped with an error log.
    /// </summary>
    public class WarehouseRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "warehouse_exchange";
        private const string QueueName = "accounting-service.warehouse-events";
        private const string MovementKey = "warehouse.StockMovementRecordedEvent";
        private const string AttemptHeader = "x-attempt";
        private const int MaxAttempts = 5;

        private readonly IConnection _connection;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WarehouseRabbitMqConsumer> _logger;
        private IModel? _channel;

        public WarehouseRabbitMqConsumer(IConnection connection, IServiceProvider serviceProvider, ILogger<WarehouseRabbitMqConsumer> logger)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
            _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(QueueName, ExchangeName, MovementKey);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnMessageReceivedAsync;
            _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("WarehouseRabbitMqConsumer subscribed to {Exchange} via {Queue}", ExchangeName, QueueName);
            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            try
            {
                await DispatchAsync(ea.RoutingKey, Encoding.UTF8.GetString(ea.Body.ToArray()));
            }
            catch (Exception ex)
            {
                var attempt = ReadAttempt(ea.BasicProperties) + 1;
                if (attempt < MaxAttempts)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt} of {Max} failed for {RoutingKey}; republishing", attempt, MaxAttempts, ea.RoutingKey);
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                    var properties = _channel!.CreateBasicProperties();
                    properties.Persistent = true;
                    properties.Headers = new Dictionary<string, object> { [AttemptHeader] = attempt };
                    _channel.BasicPublish(ExchangeName, ea.RoutingKey, properties, ea.Body);
                }
                else
                {
                    _logger.LogError(ex, "Giving up on {RoutingKey} after {Max} attempts; the message is dropped. Payload: {Payload}",
                        ea.RoutingKey, MaxAttempts, Truncate(Encoding.UTF8.GetString(ea.Body.ToArray())));
                }
            }

            // Either way this delivery is finished: the retry (if any) is a new message.
            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }

        private async Task DispatchAsync(string routingKey, string json)
        {
            if (routingKey != MovementKey)
            {
                _logger.LogWarning("Unhandled routing key {RoutingKey} on {Exchange}", routingKey, ExchangeName);
                return;
            }

            var movement = ParseMovement(json);
            if (movement == null)
                return;

            if (!EventTenant.TryRead(json, out var tenantId))
            {
                _logger.LogWarning("StockMovementRecorded {MovementId} without TenantId ignored", movement.MovementId);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            using (TenantScope.Begin(tenantId))
                await mediator.Publish(movement);
        }

        /// <summary>Reads the fields the ledger needs; null when the payload has no movement id. ValueDelta is absent (0) on events
        /// published before the cost fields existed, which books nothing.</summary>
        public static StockMovementRecordedIntegrationEvent? ParseMovement(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("MovementId", out var id) || !id.TryGetGuid(out var movementId))
                return null;

            return new StockMovementRecordedIntegrationEvent
            {
                MovementId = movementId,
                MovementType = root.TryGetProperty("MovementType", out var type) && type.ValueKind == JsonValueKind.String
                    ? type.GetString() ?? string.Empty
                    : string.Empty,
                ValueDelta = root.TryGetProperty("ValueDelta", out var delta) && delta.ValueKind == JsonValueKind.Number
                    ? delta.GetDecimal()
                    : 0m,
                Reference = root.TryGetProperty("Reference", out var reference) && reference.ValueKind == JsonValueKind.String
                    ? reference.GetString()
                    : null,
                RecordedAt = root.TryGetProperty("RecordedAt", out var at) && at.TryGetDateTime(out var recorded) ? recorded : default,
            };
        }

        private static int ReadAttempt(IBasicProperties? properties) =>
            properties?.Headers != null && properties.Headers.TryGetValue(AttemptHeader, out var value)
                ? Convert.ToInt32(value)
                : 0;

        private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000] + "...";

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _channel?.Dispose();
            return Task.CompletedTask;
        }
    }
}
