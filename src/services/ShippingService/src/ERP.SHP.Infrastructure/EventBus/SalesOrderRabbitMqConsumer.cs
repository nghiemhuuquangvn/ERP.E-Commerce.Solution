using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using ERP.SHP.Application.Interfaces;
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

                // CreateShipmentCommand carries the saga's (order's) tenant; without one nothing is
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
                    case "sales_order.CreateShipmentCommand":
                        var evt = JsonSerializer.Deserialize<SagaCreateShipmentCommand>(json);
                        if (evt == null)
                            break;

                        // Never create a shipment the carrier can't deliver: a command without
                        // an address (sent by a SaleOrderService older than this consumer) is
                        // logged and dropped instead of creating a placeholder shipment.
                        if (!evt.HasDeliveryAddress)
                        {
                            _logger.LogWarning("CreateShipmentCommand for order {OrderId} has no delivery address — shipment not created", evt.OrderId);
                            break;
                        }

                        // The tenant's own default carrier (first active one by name) — carriers
                        // are per tenant; this used to be a hard-coded SYSTEM-tenant carrier id.
                        var carriers = await scope.ServiceProvider.GetRequiredService<ICarrierRepository>().GetActiveAsync();
                        var carrier = carriers.OrderBy(c => c.Name).FirstOrDefault();
                        if (carrier == null)
                        {
                            _logger.LogWarning("No active carrier in tenant {TenantId} — shipment for order {OrderId} not created", tenantId, evt.OrderId);
                            break;
                        }

                        await mediator.Send(evt.ToCreateShipmentCommand(carrier.Id));
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
        public string RecipientName { get; set; } = string.Empty;
        public string? RecipientEmail { get; set; }
        public SagaShippingAddress? ShippingAddress { get; set; }
        public List<SagaShipmentLine> Items { get; set; } = new();

        public bool HasDeliveryAddress =>
            ShippingAddress != null && !string.IsNullOrWhiteSpace(ShippingAddress.Street);

        /// <summary>
        /// Sales orders keep a generic Street/City/State address (no ward, no phone yet — see
        /// docs/ROADMAP.md P2-2): City is the district and State the province of a Vietnamese
        /// address, which is how the storefront checkout fills them.
        /// </summary>
        public CreateShipmentCommand ToCreateShipmentCommand(Guid carrierId) => new()
        {
            OrderId = OrderId,
            CarrierId = carrierId,
            Recipient = new ShipmentRecipientRequest
            {
                RecipientName = RecipientName,
                AddressStreet = ShippingAddress?.Street ?? string.Empty,
                AddressDistrict = ShippingAddress?.City ?? string.Empty,
                AddressProvince = ShippingAddress?.State ?? string.Empty,
                AddressPostalCode = string.IsNullOrWhiteSpace(ShippingAddress?.PostalCode) ? null : ShippingAddress.PostalCode,
                AddressCountry = string.IsNullOrWhiteSpace(ShippingAddress?.Country) ? "Vietnam" : ShippingAddress.Country
            },
            EstimatedDelivery = DateTime.UtcNow.AddDays(5),
            Items = Items.Select(i => new ShipmentItemRequest
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity
            }).ToList()
        };
    }

    public class SagaShippingAddress
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    public class SagaShipmentLine
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
