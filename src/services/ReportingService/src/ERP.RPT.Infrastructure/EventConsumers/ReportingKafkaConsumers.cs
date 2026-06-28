using Confluent.Kafka;
using ERP.RPT.Domain.Models;
using ERP.RPT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ERP.RPT.Infrastructure.EventConsumers;

// ── Integration Event Contracts ────────────────────────────────────────────

public record SalesOrderCreatedEvent(Guid OrderId, Guid CustomerId, string CustomerName, decimal TotalAmount, int ItemCount, DateTime OrderDate);
public record SalesOrderApprovedEvent(Guid OrderId, DateTime ApprovedAt);
public record SalesOrderShippedEvent(Guid OrderId, DateTime ShippedAt, string TrackingNumber);
public record SalesOrderDeliveredEvent(Guid OrderId, DateTime DeliveredAt);
public record PaymentProcessedEvent(Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string Method, DateTime ProcessedAt);
public record PaymentRefundedEvent(Guid PaymentId, Guid OrderId, decimal Amount);
public record InventoryUpdatedEvent(Guid MovementId, Guid ProductId, string MovementType, int Quantity, DateTime OccurredAt);
public record ProductCreatedEvent(Guid ProductId, string Name, string Category, decimal Price);
public record EmployeeHiredEvent(Guid EmployeeId, string FirstName, string LastName, Guid DepartmentId, DateTime DateOfHire);

// ── ETL Helper ─────────────────────────────────────────────────────────────

internal static class DateKeyHelper
{
    internal static int ToDateKey(DateTime date) =>
        date.Year * 10000 + date.Month * 100 + date.Day;
}

// ── Kafka Consumer Background Services ─────────────────────────────────────

/// <summary>
/// Consumes SalesOrder events from Kafka → writes to FactSalesOrders table.
/// Handles: SalesOrderCreated, SalesOrderApproved, SalesOrderShipped, SalesOrderDelivered
/// </summary>
public class SalesOrderEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<SalesOrderEventConsumer> _logger;

    private static readonly string[] Topics = { "sales-order-created", "sales-order-approved", "sales-order-shipped", "sales-order-delivered" };

    public SalesOrderEventConsumer(IServiceProvider services, IConfiguration config, ILogger<SalesOrderEventConsumer> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaServers = _config["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaServers,
            GroupId = "reporting-sales-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(Topics);

        _logger.LogInformation("[REPORTING] SalesOrderEventConsumer started, subscribing to {Topics}", string.Join(", ", Topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await ProcessSalesOrderEventAsync(result.Topic, result.Message.Value, stoppingToken);
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on sales-order topics");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        consumer.Close();
    }

    private async Task ProcessSalesOrderEventAsync(string topic, string json, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        try
        {
            switch (topic)
            {
                case "sales-order-created":
                {
                    var e = JsonSerializer.Deserialize<SalesOrderCreatedEvent>(json);
                    if (e == null) return;

                    var existing = await db.FactSalesOrders.FindAsync(new object[] { e.OrderId }, ct);
                    if (existing != null) return;

                    db.FactSalesOrders.Add(new FactSalesOrder
                    {
                        OrderId = e.OrderId,
                        CustomerId = e.CustomerId,
                        CustomerName = e.CustomerName,
                        Status = "Created",
                        TotalAmount = e.TotalAmount,
                        ItemCount = e.ItemCount,
                        OrderDate = e.OrderDate,
                        DateKey = DateKeyHelper.ToDateKey(e.OrderDate)
                    });
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("[REPORTING] FactSalesOrder inserted: {OrderId}", e.OrderId);
                    break;
                }

                case "sales-order-approved":
                {
                    var e = JsonSerializer.Deserialize<SalesOrderApprovedEvent>(json);
                    if (e == null) return;

                    var row = await db.FactSalesOrders.FindAsync(new object[] { e.OrderId }, ct);
                    if (row == null) return;

                    row.Status = "Approved";
                    row.ApprovedAt = e.ApprovedAt;
                    await db.SaveChangesAsync(ct);
                    break;
                }

                case "sales-order-shipped":
                {
                    var e = JsonSerializer.Deserialize<SalesOrderShippedEvent>(json);
                    if (e == null) return;

                    var row = await db.FactSalesOrders.FindAsync(new object[] { e.OrderId }, ct);
                    if (row == null) return;

                    row.Status = "Shipped";
                    row.ShippedAt = e.ShippedAt;
                    await db.SaveChangesAsync(ct);
                    break;
                }

                case "sales-order-delivered":
                {
                    var e = JsonSerializer.Deserialize<SalesOrderDeliveredEvent>(json);
                    if (e == null) return;

                    var row = await db.FactSalesOrders.FindAsync(new object[] { e.OrderId }, ct);
                    if (row == null) return;

                    row.Status = "Delivered";
                    row.DeliveredAt = e.DeliveredAt;
                    await db.SaveChangesAsync(ct);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Error processing {Topic}: {Json}", topic, json[..Math.Min(200, json.Length)]);
        }
    }
}

/// <summary>
/// Consumes Payment events from Kafka → writes to FactPayments table.
/// </summary>
public class PaymentEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentEventConsumer> _logger;

    private static readonly string[] Topics = { "payment-processed", "payment-refunded" };

    public PaymentEventConsumer(IServiceProvider services, IConfiguration config, ILogger<PaymentEventConsumer> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaServers = _config["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaServers,
            GroupId = "reporting-payment-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(Topics);
        _logger.LogInformation("[REPORTING] PaymentEventConsumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                if (result.Topic == "payment-processed")
                {
                    var e = JsonSerializer.Deserialize<PaymentProcessedEvent>(result.Message.Value);
                    if (e == null) continue;

                    var existing = await db.FactPayments.FindAsync(new object[] { e.PaymentId }, stoppingToken);
                    if (existing != null) continue;

                    db.FactPayments.Add(new FactPayment
                    {
                        PaymentId = e.PaymentId,
                        OrderId = e.OrderId,
                        Amount = e.Amount,
                        Currency = e.Currency,
                        Method = e.Method,
                        Status = "Processed",
                        ProcessedAt = e.ProcessedAt,
                        DateKey = DateKeyHelper.ToDateKey(e.ProcessedAt)
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] FactPayment inserted: {PaymentId}", e.PaymentId);
                }
                else if (result.Topic == "payment-refunded")
                {
                    var e = JsonSerializer.Deserialize<PaymentRefundedEvent>(result.Message.Value);
                    if (e == null) continue;

                    var row = await db.FactPayments.FindAsync(new object[] { e.PaymentId }, stoppingToken);
                    if (row != null) { row.Status = "Refunded"; await db.SaveChangesAsync(stoppingToken); }
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on payment topics");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>
/// Consumes Inventory events from Kafka → writes to FactInventoryMovements table.
/// </summary>
public class InventoryEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<InventoryEventConsumer> _logger;

    public InventoryEventConsumer(IServiceProvider services, IConfiguration config, ILogger<InventoryEventConsumer> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaServers = _config["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaServers,
            GroupId = "reporting-inventory-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(new[] { "inventory-updated", "stock-moved" });
        _logger.LogInformation("[REPORTING] InventoryEventConsumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                var e = JsonSerializer.Deserialize<InventoryUpdatedEvent>(result.Message.Value);
                if (e == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                var existing = await db.FactInventoryMovements.FindAsync(new object[] { e.MovementId }, stoppingToken);
                if (existing != null) continue;

                db.FactInventoryMovements.Add(new FactInventoryMovement
                {
                    MovementId = e.MovementId,
                    ProductId = e.ProductId,
                    MovementType = e.MovementType,
                    Quantity = e.Quantity,
                    OccurredAt = e.OccurredAt,
                    DateKey = DateKeyHelper.ToDateKey(e.OccurredAt)
                });
                await db.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("[REPORTING] FactInventoryMovement inserted: {MovementId}", e.MovementId);
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on inventory topics");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>
/// Registration extension for all Reporting Kafka consumers.
/// </summary>
public static class ReportingConsumerExtensions
{
    public static IServiceCollection AddReportingKafkaConsumers(this IServiceCollection services)
    {
        services.AddHostedService<SalesOrderEventConsumer>();
        services.AddHostedService<PaymentEventConsumer>();
        services.AddHostedService<InventoryEventConsumer>();
        return services;
    }
}
