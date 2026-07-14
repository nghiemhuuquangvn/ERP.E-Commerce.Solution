using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.SO.Application.Sagas;

namespace ERP.SO.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes WarehouseService's "warehouse_exchange" topic exchange (RabbitMQ,
    /// plain System.Text.Json — no "$type", matches WarehouseService's EventPublisher).
    /// Feeds OrderFulfillmentSaga's HandleInventoryReservedAsync/
    /// HandleInventoryInsufficientAsync — previously nothing in SaleOrderService
    /// consumed this exchange at all.
    /// </summary>
    public class WarehouseRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "warehouse_exchange";
        private const string QueueName = "sales-order-service.warehouse-events";

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
            _channel.QueueBind(QueueName, ExchangeName, "warehouse.InventoryInsufficientEvent");

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
                var saga = scope.ServiceProvider.GetRequiredService<OrderFulfillmentSaga>();

                switch (ea.RoutingKey)
                {
                    case "warehouse.InventoryReservedEvent":
                        var reserved = JsonSerializer.Deserialize<InventoryReservedEventContract>(json);
                        if (reserved != null)
                            await saga.HandleInventoryReservedAsync(reserved.OrderId, reserved.ReservationId, default);
                        break;

                    case "warehouse.InventoryInsufficientEvent":
                        var insufficient = JsonSerializer.Deserialize<InventoryInsufficientEventContract>(json);
                        if (insufficient != null)
                            await saga.HandleInventoryInsufficientAsync(insufficient.OrderId, insufficient.Reason, default);
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

    // Local mirrors of WarehouseService's published contracts — only the fields the
    // saga needs are declared; JsonSerializer ignores the rest.
    internal class InventoryReservedEventContract
    {
        public Guid ReservationId { get; set; }
        public Guid OrderId { get; set; }
    }

    internal class InventoryInsufficientEventContract
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
