using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.SHP.Application.EventHandlers;

namespace ERP.SHP.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes WarehouseService's "warehouse_exchange" topic exchange (RabbitMQ —
    /// WarehouseService publishes via a direct RabbitMQ EventPublisher, not Kafka/Outbox).
    /// This is the previously-missing "InventoryReserved event consumer" gap. Plain
    /// System.Text.Json (no "$type"), dispatch by routing key.
    /// </summary>
    public class WarehouseRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "warehouse_exchange";
        private const string QueueName = "shipping-service.warehouse-events";

        private readonly IConnection _connection;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WarehouseRabbitMqConsumer> _logger;
        private IModel? _channel;

        public WarehouseRabbitMqConsumer(
            IConnection connection,
            IServiceProvider serviceProvider,
            ILogger<WarehouseRabbitMqConsumer> logger)
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
            _channel.QueueBind(QueueName, ExchangeName, "warehouse.InventoryReservedEvent");

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
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                switch (ea.RoutingKey)
                {
                    case "warehouse.InventoryReservedEvent":
                        var evt = JsonSerializer.Deserialize<InventoryReservedIntegrationEvent>(json);
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
