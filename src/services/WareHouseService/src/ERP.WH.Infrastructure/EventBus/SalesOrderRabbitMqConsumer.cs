using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.EventHandlers;

namespace ERP.WH.Infrastructure.EventBus;

/// <summary>
/// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that
/// service publishes via RabbitMqMessageBus, not Kafka/Outbox like most other
/// services). Dispatches by routing key (SaleOrderService's publisher sets it to
/// "sales_order.{EventType}" using the plain CLR class name, and serializes with
/// plain System.Text.Json — no "$type" discriminator, unlike the Newtonsoft-based
/// Outbox pattern used elsewhere) to the matching locally-defined event contract in
/// Application/EventHandlers/*.cs, then publishes it through MediatR so the
/// existing (previously unreachable) INotificationHandler implementations run.
///
/// 2026-07-14: inventory reservation is now driven by OrderFulfillmentSaga's own
/// "sales_order.ReserveInventoryCommand" (published only once payment is confirmed),
/// not directly off order creation — retired the old SalesOrderCreatedEvent binding
/// that used to trigger reservation immediately, to avoid double-reserving stock.
/// </summary>
public class SalesOrderRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "sales_order_events";
    private const string QueueName = "warehouse-service.sales-order-events";

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

        _channel.QueueBind(QueueName, ExchangeName, "sales_order.ReserveInventoryCommand");
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderCancelledEvent");
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderShippedEvent");

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

            // Saga commands and Sales events carry the order's tenant; without one nothing
            // is reserved or released (fail closed) — the message is acknowledged and logged.
            if (!EventTenant.TryRead(json, out var tenantId))
            {
                _logger.LogWarning("{RoutingKey} without TenantId ignored", ea.RoutingKey);
                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                return;
            }
            using var tenantScope = TenantScope.Begin(tenantId);

            switch (ea.RoutingKey)
            {
                case "sales_order.ReserveInventoryCommand":
                    var reserve = JsonSerializer.Deserialize<SagaReserveInventoryCommand>(json);
                    if (reserve != null)
                    {
                        await mediator.Send(new ReserveInventoryCommand
                        {
                            OrderId = reserve.OrderId,
                            Items = reserve.Items.Select(i => new ReservationItemDto { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
                            HoldUntil = DateTime.UtcNow.AddDays(7),
                            CorrelationId = reserve.OrderId.ToString()
                        });
                    }
                    break;

                case "sales_order.SalesOrderCancelledEvent":
                    var cancelled = JsonSerializer.Deserialize<SalesOrderCancelledEvent>(json);
                    if (cancelled != null) await mediator.Publish(cancelled);
                    break;

                case "sales_order.SalesOrderShippedEvent":
                    var shipped = JsonSerializer.Deserialize<SalesOrderShippedEvent>(json);
                    if (shipped != null) await mediator.Publish(shipped);
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

// Local mirror of OrderFulfillmentSaga's own ReserveInventoryCommand message
// (ERP.SO.Application.Sagas.ReserveInventoryCommand) — only the fields WH needs.
public class SagaReserveInventoryCommand
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public List<SagaLineItemContract> Items { get; set; } = new();
}

public class SagaLineItemContract
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
