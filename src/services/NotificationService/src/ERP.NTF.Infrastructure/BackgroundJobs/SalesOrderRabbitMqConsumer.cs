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
/// Binds to SalesOrderCreated / Approved / Shipped / Delivered / Cancelled / Returned: the handlers write the order's recipient
/// (from Created) and send the customer's order confirmation, shipped, delivered, cancelled and returned messages (ROADMAP P1-6).
/// A failing message is retried 5 times with an attempt header instead of being requeued for ever (as in P1-5).
/// </summary>
public class SalesOrderRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "sales_order_events";
    private const string QueueName = "notification-service.sales-order-events";
    private const string AttemptHeader = "x-attempt";
    private const int MaxAttempts = 5;

    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SalesOrderRabbitMqConsumer> _logger;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
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
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderShippedEvent");
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderDeliveredEvent");
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderCancelledEvent");
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.SalesOrderReturnedEvent");

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
                    var created = JsonSerializer.Deserialize<SalesOrderCreatedIntegrationEvent>(json, Options);
                    if (created != null) await mediator.Publish(created);
                    break;

                case "sales_order.SalesOrderApprovedEvent":
                    var approved = JsonSerializer.Deserialize<SalesOrderApprovedIntegrationEvent>(json, Options);
                    if (approved != null) await mediator.Publish(approved);
                    break;

                case "sales_order.SalesOrderShippedEvent":
                    var shipped = JsonSerializer.Deserialize<SalesOrderShippedIntegrationEvent>(json, Options);
                    if (shipped != null) await mediator.Publish(shipped);
                    break;

                case "sales_order.SalesOrderDeliveredEvent":
                    var delivered = JsonSerializer.Deserialize<SalesOrderDeliveredIntegrationEvent>(json, Options);
                    if (delivered != null) await mediator.Publish(delivered);
                    break;

                case "sales_order.SalesOrderCancelledEvent":
                    var cancelled = JsonSerializer.Deserialize<SalesOrderCancelledIntegrationEvent>(json, Options);
                    if (cancelled != null) await mediator.Publish(cancelled);
                    break;

                case "sales_order.SalesOrderReturnedEvent":
                    var returned = JsonSerializer.Deserialize<SalesOrderReturnedIntegrationEvent>(json, Options);
                    if (returned != null) await mediator.Publish(returned);
                    break;

                default:
                    _logger.LogWarning("Unhandled routing key {Key} on {Exchange}", ea.RoutingKey, ExchangeName);
                    break;
            }

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            // Republish with an attempt counter instead of requeueing for ever; the handlers are idempotent (dedup keys).
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
                _logger.LogError(ex, "Giving up on {RoutingKey} after {Max} attempts; the message is dropped. Payload: {Payload}",
                    ea.RoutingKey, MaxAttempts, Encoding.UTF8.GetString(ea.Body.ToArray()));
            }
            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
    }

    private static int ReadAttempt(IBasicProperties properties)
    {
        if (properties?.Headers != null && properties.Headers.TryGetValue(AttemptHeader, out var value))
            return value switch { int i => i, long l => (int)l, byte[] b when int.TryParse(Encoding.UTF8.GetString(b), out var parsed) => parsed, _ => 0 };
        return 0;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _channel?.Dispose();
        return Task.CompletedTask;
    }
}
