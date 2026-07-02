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
        private const string ExchangeName = "sales_order_events";
        private const string QueueName = "promotion-service.sales-order-events";

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

                switch (ea.RoutingKey)
                {
                    case "sales_order.SalesOrderCreatedEvent":
                        var raw = JObject.Parse(json);
                        var lines = (raw["LineItems"] as JArray)?.Select(li => new OrderLineDto(
                            li.Value<Guid>("ProductId"),
                            li.Value<int>("Quantity"),
                            li.Value<decimal>("UnitPrice"))).ToList() ?? new List<OrderLineDto>();

                        var evt = new SalesOrderCreatedIntegrationEvent(
                            raw.Value<Guid>("OrderId"),
                            raw.Value<Guid>("CustomerId"),
                            raw.Value<decimal>("TotalAmount"),
                            lines);

                        var handler = scope.ServiceProvider.GetRequiredService<SalesOrderCreatedForPromotionHandler>();
                        await handler.HandleAsync(evt, CancellationToken.None);
                        break;

                    default:
                        _logger.LogWarning("Unhandled routing key {RoutingKey} on {Exchange}", ea.RoutingKey, ExchangeName);
                        break;
                }

                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message with routing key {RoutingKey} from {Exchange}", ea.RoutingKey, ExchangeName);
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _channel?.Dispose();
            return Task.CompletedTask;
        }
    }
}
