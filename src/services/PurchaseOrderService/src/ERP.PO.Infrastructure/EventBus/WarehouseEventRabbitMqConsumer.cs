using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.PO.Application.Features.PurchaseRequests;

namespace ERP.PO.Infrastructure.EventBus;

/// <summary>
/// First-ever event consumer in this service (previously outbound-only via
/// KafkaMessageBus/OutboxProcessor). Binds to WareHouseService's own
/// "warehouse_exchange" topic exchange (RabbitMQ — WareHouseService does not publish
/// via Kafka) on routing key "warehouse.StockAlertEvent", deserializing with plain
/// System.Text.Json to match EventPublisher.PublishAsync's own JsonSerializer.Serialize
/// call — no "$type" discriminator, unlike the Newtonsoft Outbox pattern used elsewhere.
/// </summary>
public class WarehouseEventRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "warehouse_exchange";
    private const string QueueName = "purchasing-service.warehouse-events";
    private const string RoutingKey = "warehouse.StockAlertEvent";

    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WarehouseEventRabbitMqConsumer> _logger;
    private IModel? _channel;

    public WarehouseEventRabbitMqConsumer(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<WarehouseEventRabbitMqConsumer> logger)
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
        _channel.QueueBind(QueueName, ExchangeName, RoutingKey);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;

        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("WarehouseEventRabbitMqConsumer subscribed to {Exchange} via {Queue}", ExchangeName, QueueName);

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var payload = JsonSerializer.Deserialize<StockAlertEventPayload>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new CreatePurchaseRequestFromStockAlertCommand
                {
                    ProductId = payload.ProductId,
                    ProductName = payload.ProductName,
                    CurrentQuantity = payload.CurrentQuantity,
                    ReorderPoint = payload.ReorderPoint,
                    ReorderQuantity = payload.ReorderQuantity
                });
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

/// <summary>Local mirror of WareHouseService's own StockAlertEvent — only the fields this service needs.</summary>
public class StockAlertEventPayload
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
}
