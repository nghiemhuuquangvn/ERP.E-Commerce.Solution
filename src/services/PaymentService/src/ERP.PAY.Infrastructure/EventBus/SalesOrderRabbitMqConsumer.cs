using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.PAY.Application.EventHandlers;

namespace ERP.PAY.Infrastructure.EventBus;

/// <summary>
/// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that
/// service publishes via RabbitMqMessageBus, not Kafka/Outbox like most other
/// services). Only the "sales_order.SalesOrderCreatedEvent" routing key is relevant
/// to Payment (a new order needs a payment initiated). Deserializes (plain
/// System.Text.Json — no "$type" discriminator, since SaleOrderService's publisher
/// uses plain JsonSerializer.Serialize) directly into the existing local
/// SalesOrderCreatedIntegrationEvent contract (Application/EventHandlers/
/// SalesOrderCreatedEventHandler.cs), then dispatches through MediatR so the
/// existing (previously unreachable) SalesOrderCreatedIntegrationEventHandler runs.
///
/// Note: SaleOrderService's real SalesOrderCreatedEvent does not carry a Currency or
/// PaymentMethod field, so those two properties on the local event simply keep their
/// default values ("USD" / "CreditCard") after deserialization — matching fields
/// (OrderId, TotalAmount) bind correctly by name.
/// </summary>
public class SalesOrderRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "sales_order_events";
    private const string QueueName = "payment-service.sales-order-events";

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
                    var created = JsonSerializer.Deserialize<SalesOrderCreatedIntegrationEvent>(json);
                    if (created != null) await mediator.Publish(created);
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
