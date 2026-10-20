using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using System.Text;
using System.Text.Json;
using ERP.RPT.Domain.Models;
using ERP.RPT.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ERP.RPT.Infrastructure.EventConsumers;

/// <summary>
/// Consumes SaleOrderService's "sales_order_events" RabbitMQ topic exchange for
/// reporting ETL. SaleOrderService publishes via RabbitMQ (not Kafka), so the Kafka
/// SalesOrderEventConsumer placeholder in ReportingKafkaConsumers.cs will never receive
/// messages. This consumer is the authoritative source of sales order facts for the
/// reporting data warehouse.
/// </summary>
public class ReportingSalesOrderRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "sales_order_events";
    private const string QueueName = "reporting-service.sales-order-events";

    private readonly IConnection _connection;
    private readonly IServiceProvider _services;
    private readonly ILogger<ReportingSalesOrderRabbitMqConsumer> _logger;
    private IModel? _channel;

    public ReportingSalesOrderRabbitMqConsumer(
        IConnection connection,
        IServiceProvider services,
        ILogger<ReportingSalesOrderRabbitMqConsumer> logger)
    {
        _connection = connection;
        _services = services;
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

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;
        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("[REPORTING] SalesOrder RabbitMQ consumer subscribed to {Exchange}", ExchangeName);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

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
                    var orderId = JDoc.GetGuid(root, "OrderId", "orderId", "Id", "id");
                    if (orderId != Guid.Empty)
                    {
                        var existing = await db.FactSalesOrders.FindAsync(orderId);
                        if (existing == null)
                        {
                            var orderDate = JDoc.GetDateTime(root, "OrderDate", "orderDate", "CreatedAt");
                            db.FactSalesOrders.Add(new FactSalesOrder
                            {
                                OrderId = orderId,
                                CustomerId = JDoc.GetGuid(root, "CustomerId", "customerId"),
                                CustomerName = JDoc.GetString(root, "CustomerName", "customerName"),
                                Status = "Created",
                                TotalAmount = JDoc.GetDecimal(root, "TotalAmount", "totalAmount"),
                                ItemCount = JDoc.GetInt(root, "ItemCount", "itemCount"),
                                OrderDate = orderDate,
                                DateKey = DateKeyHelper.ToDateKey(orderDate)
                            });
                            await db.SaveChangesAsync();
                        }
                    }
                    break;

                case "sales_order.SalesOrderApprovedEvent":
                    var approvedId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var approvedRow = approvedId != Guid.Empty ? await db.FactSalesOrders.FindAsync(approvedId) : null;
                    if (approvedRow != null)
                    {
                        approvedRow.Status = "Approved";
                        approvedRow.ApprovedAt = JDoc.GetDateTime(root, "ApprovedAt", "approvedAt");
                        await db.SaveChangesAsync();
                    }
                    break;

                case "sales_order.SalesOrderShippedEvent":
                    var shippedId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var shippedRow = shippedId != Guid.Empty ? await db.FactSalesOrders.FindAsync(shippedId) : null;
                    if (shippedRow != null)
                    {
                        shippedRow.Status = "Shipped";
                        shippedRow.ShippedAt = JDoc.GetDateTime(root, "ShippedAt", "shippedAt");
                        await db.SaveChangesAsync();
                    }
                    break;

                case "sales_order.SalesOrderDeliveredEvent":
                    var deliveredId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var deliveredRow = deliveredId != Guid.Empty ? await db.FactSalesOrders.FindAsync(deliveredId) : null;
                    if (deliveredRow != null)
                    {
                        deliveredRow.Status = "Delivered";
                        deliveredRow.DeliveredAt = JDoc.GetDateTime(root, "DeliveredAt", "deliveredAt");
                        await db.SaveChangesAsync();
                    }
                    break;

                case "sales_order.SalesOrderCancelledEvent":
                    var cancelledId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var cancelledRow = cancelledId != Guid.Empty ? await db.FactSalesOrders.FindAsync(cancelledId) : null;
                    if (cancelledRow != null)
                    {
                        cancelledRow.Status = "Cancelled";
                        await db.SaveChangesAsync();
                    }
                    break;
            }

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Error processing {RoutingKey} from {Exchange}", ea.RoutingKey, ExchangeName);
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
