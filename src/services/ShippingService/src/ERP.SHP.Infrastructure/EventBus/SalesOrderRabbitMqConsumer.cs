using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.SHP.Application.Commands;

namespace ERP.SHP.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that
    /// service publishes via RabbitMqMessageBus, not Kafka/Outbox). Mirrors
    /// WareHouseService's SalesOrderRabbitMqConsumer.
    ///
    /// 2026-07-14: shipment creation is now driven by OrderFulfillmentSaga's own
    /// "sales_order.CreateShipmentCommand" (published once the order is both
    /// Approved and has inventory reserved), not directly off order approval —
    /// retired the old SalesOrderApprovedEvent binding to avoid double-creating
    /// shipments. Recipient/address are still placeholders (parity with the retired
    /// handler, not a regression) — SO's events still don't carry real address data.
    /// </summary>
    public class SalesOrderRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "sales_order_events";
        private const string QueueName = "shipping-service.sales-order-events";

        private static readonly Guid DefaultCarrierId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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
            _channel.QueueBind(QueueName, ExchangeName, "sales_order.CreateShipmentCommand");

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
                    case "sales_order.CreateShipmentCommand":
                        var evt = JsonSerializer.Deserialize<SagaCreateShipmentCommand>(json);
                        if (evt != null)
                        {
                            await mediator.Send(new CreateShipmentCommand
                            {
                                OrderId = evt.OrderId,
                                CarrierId = DefaultCarrierId,
                                Recipient = new ShipmentRecipientRequest
                                {
                                    RecipientName = "(pending address sync)",
                                    AddressStreet = "(pending address sync)",
                                    AddressDistrict = "(pending)",
                                    AddressProvince = "(pending)"
                                },
                                EstimatedDelivery = DateTime.UtcNow.AddDays(5),
                                Items = new List<ShipmentItemRequest>()
                            });
                        }
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

    // Local mirror of OrderFulfillmentSaga's own CreateShipmentCommand message
    // (ERP.SO.Application.Sagas.CreateShipmentCommand) — only the fields Shipping needs.
    public class SagaCreateShipmentCommand
    {
        public Guid OrderId { get; set; }
        public Guid TenantId { get; set; }
    }
}
