using System.Text;
using System.Text.Json;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ERP.PRD.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes WarehouseService's "warehouse_exchange" topic exchange (RabbitMQ,
    /// plain System.Text.Json — no "$type" discriminator, same convention as
    /// SaleOrderService's bus) to keep Product.StockQuantity as a read-optimized
    /// projection for storefront display, without a synchronous call to Warehouse on
    /// every catalog read. SQL in WarehouseService remains the system-of-record for
    /// inventory; this is a rebuildable cache, same eventual-consistency shape as the
    /// Elasticsearch search index.
    /// </summary>
    public class WarehouseRabbitMqConsumer : IHostedService
    {
        private const string ExchangeName = "warehouse_exchange";
        private const string QueueName = "product-service.inventory-updated";
        private const string RoutingKey = "warehouse.InventoryUpdatedEvent";

        private readonly IConnection _connection;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WarehouseRabbitMqConsumer> _logger;
        private IModel? _channel;

        public WarehouseRabbitMqConsumer(IConnection connection, IServiceProvider serviceProvider, ILogger<WarehouseRabbitMqConsumer> logger)
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

            _logger.LogInformation("WarehouseRabbitMqConsumer subscribed to {Exchange} via {Queue}", ExchangeName, QueueName);
            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var payload = JsonSerializer.Deserialize<InventoryUpdatedPayload>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (payload != null && payload.ProductId != Guid.Empty)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == payload.ProductId && !p.IsDeleted);
                    if (product != null)
                    {
                        product.SyncStockQuantity(payload.CurrentQuantity);
                        await context.SaveChangesAsync();
                    }
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

        /// <summary>Mirrors only the fields this consumer needs from WH.Domain.Events.InventoryUpdatedEvent (ProductId, CurrentQuantity) — not a shared contract, per this system's convention of each consumer defining its own minimal local shape.</summary>
        private class InventoryUpdatedPayload
        {
            public Guid ProductId { get; set; }
            public int CurrentQuantity { get; set; }
        }
    }
}
