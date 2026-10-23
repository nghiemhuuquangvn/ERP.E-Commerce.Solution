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
    /// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that service publishes via
    /// RabbitMqMessageBus, not Kafka/Outbox like most other services). Plain System.Text.Json, no "$type"; dispatch by routing key:
    ///   SalesOrderCreatedEvent   -> an invoice (and its revenue entry) for the order
    ///   SalesOrderCancelledEvent -> the unpaid invoice is cancelled and its entry reversed
    /// A message that fails is republished with an attempt counter (the handlers are idempotent) and, after
    /// <see cref="MaxAttempts"/> attempts, dropped with an error log: requeueing for ever kept one poison message hot.
    /// </summary>
    public class SalesOrderRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "sales_order_events";
        private const string QueueName = "accounting-service.sales-order-events";
        private const string CreatedKey = "sales_order.SalesOrderCreatedEvent";
        private const string CancelledKey = "sales_order.SalesOrderCancelledEvent";
        private const string AttemptHeader = "x-attempt";
        private const int MaxAttempts = 5;

        private readonly IConnection _connection;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SalesOrderRabbitMqConsumer> _logger;
        private IModel? _channel;

        public SalesOrderRabbitMqConsumer(
            IConnection connection,
            IServiceProvider serviceProvider,
            ILogger<SalesOrderRabbitMqConsumer> logger)
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
            _channel.QueueBind(QueueName, ExchangeName, CreatedKey);
            _channel.QueueBind(QueueName, ExchangeName, CancelledKey);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnMessageReceivedAsync;

            _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("SalesOrderRabbitMqConsumer subscribed to {Exchange} via {Queue}", ExchangeName, QueueName);

            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            try
            {
                await DispatchAsync(ea.RoutingKey, Encoding.UTF8.GetString(ea.Body.ToArray()));
                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
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

                // Either way this delivery is finished: the retry (if any) is a new message.
                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }

        private async Task DispatchAsync(string routingKey, string json)
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            switch (routingKey)
            {
                case CreatedKey:
                    var created = JsonSerializer.Deserialize<SalesOrderCreatedIntegrationEvent>(json);
                    if (created == null)
                        break;
                    if (!EventTenant.TryRead(json, out var createdTenant))
                    {
                        _logger.LogWarning("SalesOrderCreated {OrderId} without TenantId ignored", created.OrderId);
                        break;
                    }
                    using (TenantScope.Begin(createdTenant))
                        await mediator.Publish(created);
                    break;

                case CancelledKey:
                    using (var document = JsonDocument.Parse(json))
                    {
                        var root = document.RootElement;
                        if (!root.TryGetProperty("OrderId", out var orderId) || !orderId.TryGetGuid(out var id))
                            break;
                        if (!EventTenant.TryRead(json, out var cancelledTenant))
                        {
                            _logger.LogWarning("SalesOrderCancelled {OrderId} without TenantId ignored", id);
                            break;
                        }
                        var cancelled = new SalesOrderCancelledIntegrationEvent
                        {
                            OrderId = id,
                            Reason = root.TryGetProperty("CancellationReason", out var reason) && reason.ValueKind == JsonValueKind.String
                                ? reason.GetString()
                                : null
                        };
                        using (TenantScope.Begin(cancelledTenant))
                            await mediator.Publish(cancelled);
                    }
                    break;

                default:
                    _logger.LogWarning("Unhandled routing key {RoutingKey} on {Exchange}", routingKey, ExchangeName);
                    break;
            }
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
