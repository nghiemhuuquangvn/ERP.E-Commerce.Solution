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
    /// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that
    /// service publishes via RabbitMqMessageBus, not Kafka/Outbox like most other
    /// services). Dispatch by routing key, plain System.Text.Json (no "$type"
    /// discriminator). Mirrors WareHouseService's SalesOrderRabbitMqConsumer.
    /// SaleOrderService's real event's OrderId/CustomerId/TotalAmount match Accounting's
    /// existing SalesOrderCreatedIntegrationEvent field names exactly, so it deserializes
    /// directly with no field remapping, and dispatches via IMediator.Publish so the
    /// existing (previously unreachable) SalesOrderCreatedIntegrationEventHandler runs
    /// (creates an AR invoice).
    /// </summary>
    public class SalesOrderRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "sales_order_events";
        private const string QueueName = "accounting-service.sales-order-events";

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
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                switch (ea.RoutingKey)
                {
                    case "sales_order.SalesOrderCreatedEvent":
                        var evt = JsonSerializer.Deserialize<SalesOrderCreatedIntegrationEvent>(json);
                        if (evt != null) await mediator.Publish(evt);
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
