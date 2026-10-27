using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json.Linq;
using ERP.PRM.Application.EventHandlers;

namespace ERP.PRM.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that
    /// service publishes via RabbitMqMessageBus, not Kafka/Outbox). Plain
    /// System.Text.Json on the wire (no "$type"), but this consumer parses via
    /// Newtonsoft.Json.Linq for convenience since the local
    /// SalesOrderCreatedIntegrationEvent's "Lines" property name doesn't match the
    /// real event's "LineItems" — a straight generic deserialize wouldn't populate it,
    /// so fields are mapped explicitly instead. Dispatches to the existing (previously
    /// dead) SalesOrderCreatedForPromotionHandler (earns loyalty points), resolved
    /// from a DI scope and invoked directly since it's a plain class, not a MediatR
    /// INotificationHandler.
    /// </summary>
    public class SalesOrderRabbitMqConsumer : IHostedService
    {
        /// <summary>
        /// What the customer paid for the goods: the order total without the delivery fee (ROADMAP P1-11b). Loyalty points are earned on goods, not on delivery;
        /// an event published before the fee existed has no ShippingFee and counts as 0.
        /// </summary>
        public static decimal GoodsTotal(Newtonsoft.Json.Linq.JObject raw) =>
            Math.Max(0m, raw.Value<decimal>("TotalAmount") - (raw.Value<decimal?>("ShippingFee") ?? 0m));

        private const string ExchangeName = "sales_order_events";
        private const string QueueName = "promotion-service.sales-order-events";
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
            _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderCreatedEvent");
            _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderCancelledEvent");
            _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderReturnedEvent");

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
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                using var scope = _serviceProvider.CreateScope();

                // SalesOrderCreated carries the order's tenant (stamped by SaleOrderService); without one nothing is
                // changed (fail closed) — the message is acknowledged and logged.
                if (!EventTenant.TryRead(json, out var tenantId))
                {
                    _logger.LogWarning("{RoutingKey} without TenantId ignored", ea.RoutingKey);
                    _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                switch (ea.RoutingKey)
                {
                    case "sales_order.SalesOrderCreatedEvent":
                        var raw = JObject.Parse(json);
                        var lines = raw.ArrayValue("LineItems")?.Select(li => new OrderLineDto(
                            (li.GuidValue("ProductId") ?? Guid.Empty),
                            li.Value<int>("Quantity"),
                            li.Value<decimal>("UnitPrice"))).ToList() ?? new List<OrderLineDto>();

                        var evt = new SalesOrderCreatedIntegrationEvent(
                            (raw.GuidValue("OrderId") ?? Guid.Empty),
                            (raw.GuidValue("CustomerId") ?? Guid.Empty),
                            GoodsTotal(raw),
                            lines);

                        var handler = scope.ServiceProvider.GetRequiredService<SalesOrderCreatedForPromotionHandler>();
                        await handler.HandleAsync(evt, CancellationToken.None);
                        break;

                    case "sales_order.SalesOrderCancelledEvent":
                    case "sales_order.SalesOrderReturnedEvent":
                        var reversedRaw = JObject.Parse(json);
                        var reversed = new SalesOrderReversedIntegrationEvent(
                            reversedRaw.GuidValue("OrderId") ?? Guid.Empty, reversedRaw.GuidValue("CustomerId") ?? Guid.Empty);
                        if (reversed.OrderId != Guid.Empty && reversed.CustomerId != Guid.Empty)
                            await scope.ServiceProvider.GetRequiredService<SalesOrderReversedForPromotionHandler>().HandleAsync(reversed, CancellationToken.None);
                        break;

                    default:
                        _logger.LogWarning("Unhandled routing key {RoutingKey} on {Exchange}", ea.RoutingKey, ExchangeName);
                        break;
                }

                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                // Republish with an attempt counter instead of requeueing for ever (as in P1-5); the handlers are idempotent.
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
                        ea.RoutingKey, MaxAttempts, Encoding.UTF8.GetString(ea.Body.ToArray()));
                }
                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }

        private static int ReadAttempt(IBasicProperties properties)
        {
            if (properties?.Headers != null && properties.Headers.TryGetValue(AttemptHeader, out var value))
                return value switch { int i => i, long l => (int)l, byte[] b when int.TryParse(Encoding.UTF8.GetString(b), out var parsed) => parsed, _ => 0 };
            return 0;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _channel?.Dispose();
            return Task.CompletedTask;
        }
    }
}
