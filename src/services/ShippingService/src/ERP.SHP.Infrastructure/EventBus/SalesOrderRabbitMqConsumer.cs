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
            _channel.QueueBind(QueueName, ExchangeName, "sales_order.CreateShipmentCommand");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnMessageReceivedAsync;

            _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("SalesOrderRabbitMqConsumer subscribed to {Exchange} via {Queue}", ExchangeName, QueueName);

            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            Guid? tenantId = null;
            Guid? orderId = null;
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                // CreateShipmentCommand carries the saga's (order's) tenant; without one nothing is
                // changed (fail closed) — the message is acknowledged and logged.
                if (!EventTenant.TryRead(json, out var tenant))
                {
                    _logger.LogWarning("{RoutingKey} without TenantId ignored", ea.RoutingKey);
                    _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }
                tenantId = tenant;
                using var tenantScope = TenantScope.Begin(tenant);
                using var scope = _serviceProvider.CreateScope();

                switch (ea.RoutingKey)
                {
                    case "sales_order.CreateShipmentCommand":
                        var evt = JsonSerializer.Deserialize<SagaCreateShipmentCommand>(json);
                        if (evt == null)
                            break;
                        orderId = evt.OrderId;
                        await scope.ServiceProvider.GetRequiredService<SagaShipmentRequestProcessor>().ProcessAsync(evt);
                        break;

                    default:
                        _logger.LogWarning("Unhandled routing key {RoutingKey} on {Exchange}", ea.RoutingKey, ExchangeName);
                        break;
                }

                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                // Republish with an attempt counter instead of requeueing for ever (ROADMAP P1-5); when the attempts are used up the saga is told the
                // shipment cannot be created, so it compensates instead of waiting.
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
                    _logger.LogError(ex, "Giving up on {RoutingKey} for order {OrderId} after {Max} attempts", ea.RoutingKey, orderId, MaxAttempts);
                    await TryReportFailureAsync(tenantId, orderId, ex);
                }

                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }

        private async Task TryReportFailureAsync(Guid? tenantId, Guid? orderId, Exception ex)
        {
            if (tenantId is null || orderId is null)
                return;
            try
            {
                using var tenantScope = TenantScope.Begin(tenantId.Value);
                using var scope = _serviceProvider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<SagaShipmentRequestProcessor>()
                    .ReportFailureAsync(orderId.Value, $"technical failure after {MaxAttempts} attempts: {ex.Message}");
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Could not tell Sales that the shipment of order {OrderId} failed", orderId);
            }
        }

        private static int ReadAttempt(IBasicProperties properties)
        {
            if (properties?.Headers != null && properties.Headers.TryGetValue(AttemptHeader, out var value))
            {
                return value switch { int i => i, long l => (int)l, byte[] b when int.TryParse(Encoding.UTF8.GetString(b), out var parsed) => parsed, _ => 0 };
            }
            return 0;
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
