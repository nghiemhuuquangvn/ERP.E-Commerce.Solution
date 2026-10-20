using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using System.Text;
using System.Text.Json;
using ERP.NTF.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ERP.NTF.Infrastructure.BackgroundJobs;

/// <summary>
/// Consumes SaleOrderService's "sales_order_events" RabbitMQ topic exchange.
/// Binds to SalesOrderCreated and SalesOrderApproved routing keys so the existing
/// SalesOrderCreatedIntegrationEventHandler and SalesOrderApprovedIntegrationEventHandler
/// fire and send order confirmation / approval notifications.
/// Follows AccountingService's SalesOrderRabbitMqConsumer pattern.
/// </summary>
public class SalesOrderRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "sales_order_events";
    private const string QueueName = "notification-service.sales-order-events";

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
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderApprovedEvent");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;
        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("NTF SalesOrderRabbitMqConsumer subscribed to {Exchange}", ExchangeName);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Sales events carry the order's tenant (stamped by SaleOrderService); without one nothing is
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
                    var created = JsonSerializer.Deserialize<SalesOrderCreatedIntegrationEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (created != null) await mediator.Publish(created);
                    break;

                case "sales_order.SalesOrderApprovedEvent":
                    var approved = JsonSerializer.Deserialize<SalesOrderApprovedIntegrationEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (approved != null) await mediator.Publish(approved);
                    break;

                default:
                    _logger.LogWarning("Unhandled routing key {Key} on {Exchange}", ea.RoutingKey, ExchangeName);
                    break;
            }

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RoutingKey} from {Exchange}", ea.RoutingKey, ExchangeName);
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
