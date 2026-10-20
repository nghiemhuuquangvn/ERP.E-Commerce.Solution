using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
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

// ── ETL Helper ─────────────────────────────────────────────────────────────

internal static class DateKeyHelper
{
    internal static int ToDateKey(DateTime date) =>
        date.Year * 10000 + date.Month * 100 + date.Day;
}

// ── JSON helper ────────────────────────────────────────────────────────────

internal static class JDoc
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    internal static string GetTypeName(JsonElement root)
    {
        if (root.TryGetProperty("$type", out var t)) return t.GetString() ?? "";
        return "";
    }

    internal static Guid GetGuid(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p) && p.TryGetGuid(out var g)) return g;
        return Guid.Empty;
    }

    internal static decimal GetDecimal(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p) && p.TryGetDecimal(out var d)) return d;
        return 0m;
    }

    internal static int GetInt(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p) && p.TryGetInt32(out var i)) return i;
        return 0;
    }

    internal static string GetString(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p)) return p.GetString() ?? "";
        return "";
    }

    internal static DateTime GetDateTime(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p) && p.TryGetDateTime(out var dt)) return dt;
        return DateTime.UtcNow;
    }
}

// ── Base consumer ──────────────────────────────────────────────────────────

internal abstract class ReportingConsumerBase : BackgroundService
{
    protected readonly IServiceProvider Services;
    protected readonly IConfiguration Config;
    private readonly ILogger _logger;
    private readonly string _groupId;
    private readonly string[] _topics;

    protected ReportingConsumerBase(IServiceProvider services, IConfiguration config, ILogger logger, string groupId, string[] topics)
    {
        Services = services;
        Config = config;
        _logger = logger;
        _groupId = groupId;
        _topics = topics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaServers = Config["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(_topics);
        _logger.LogInformation("[REPORTING] {Name} started, topics: {Topics}", GetType().Name, string.Join(", ", _topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] Event without TenantId ignored ({Topic})", result.Topic);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                using var doc = JsonDocument.Parse(result.Message.Value);
                await ProcessAsync(result.Topic, doc.RootElement, db, stoppingToken);
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error in {Name}", GetType().Name);
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error in {Name}", GetType().Name);
            }
        }

        consumer.Close();
    }

    protected abstract Task ProcessAsync(string topic, JsonElement root, ReportingDbContext db, CancellationToken ct);
}

// ── Sales Order Consumer ────────────────────────────────────────────────────
// NOTE: SaleOrderService currently publishes via RabbitMQ, not Kafka.
// Topics below are placeholders for when Kafka outbox is added to SaleOrderService.

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
        _logger.LogInformation("[REPORTING] SalesOrderEventConsumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;
                var typeName = JDoc.GetTypeName(root);

                // Facts belong to the tenant of the source event (stamped by the publisher's Outbox);
                // an event without one is skipped, never counted in some other tenant's reports.
                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] {EventType} without TenantId ignored", typeName);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                if (result.Topic == "sales-order-created" || typeName.Contains("SalesOrderCreated"))
                {
                    var orderId = JDoc.GetGuid(root, "OrderId", "orderId");
                    if (orderId == Guid.Empty) continue;

                    var existing = await db.FactSalesOrders.FindAsync(new object[] { orderId }, stoppingToken);
                    if (existing != null) continue;

                    db.FactSalesOrders.Add(new FactSalesOrder
                    {
                        OrderId = orderId,
                        CustomerId = JDoc.GetGuid(root, "CustomerId", "customerId"),
                        CustomerName = JDoc.GetString(root, "CustomerName", "customerName"),
                        Status = "Created",
                        TotalAmount = JDoc.GetDecimal(root, "TotalAmount", "totalAmount"),
                        ItemCount = JDoc.GetInt(root, "ItemCount", "itemCount"),
                        OrderDate = JDoc.GetDateTime(root, "OrderDate", "orderDate"),
                        DateKey = DateKeyHelper.ToDateKey(JDoc.GetDateTime(root, "OrderDate", "orderDate"))
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] FactSalesOrder inserted: {OrderId}", orderId);
                }
                else if (result.Topic == "sales-order-approved" || typeName.Contains("SalesOrderApproved"))
                {
                    var orderId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var row = await db.FactSalesOrders.FindAsync(new object[] { orderId }, stoppingToken);
                    if (row == null) continue;
                    row.Status = "Approved";
                    row.ApprovedAt = JDoc.GetDateTime(root, "ApprovedAt", "approvedAt");
                    await db.SaveChangesAsync(stoppingToken);
                }
                else if (result.Topic == "sales-order-shipped" || typeName.Contains("SalesOrderShipped"))
                {
                    var orderId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var row = await db.FactSalesOrders.FindAsync(new object[] { orderId }, stoppingToken);
                    if (row == null) continue;
                    row.Status = "Shipped";
                    row.ShippedAt = JDoc.GetDateTime(root, "ShippedAt", "shippedAt");
                    await db.SaveChangesAsync(stoppingToken);
                }
                else if (result.Topic == "sales-order-delivered" || typeName.Contains("SalesOrderDelivered"))
                {
                    var orderId = JDoc.GetGuid(root, "OrderId", "orderId");
                    var row = await db.FactSalesOrders.FindAsync(new object[] { orderId }, stoppingToken);
                    if (row == null) continue;
                    row.Status = "Delivered";
                    row.DeliveredAt = JDoc.GetDateTime(root, "DeliveredAt", "deliveredAt");
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on sales-order topics");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error on sales topics");
            }
        }

        consumer.Close();
    }
}

// ── Payment Consumer ────────────────────────────────────────────────────────
// Subscribes to "payment-topic" (PaymentService Kafka outbox).

public class PaymentEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentEventConsumer> _logger;

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
        consumer.Subscribe(new[] { "payment-topic" });
        _logger.LogInformation("[REPORTING] PaymentEventConsumer started on payment-topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;
                var typeName = JDoc.GetTypeName(root);

                // Facts belong to the tenant of the source event (stamped by the publisher's Outbox);
                // an event without one is skipped, never counted in some other tenant's reports.
                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] {EventType} without TenantId ignored", typeName);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                if (typeName.Contains("PaymentProcessed") || typeName.Contains("PaymentAuthorized") || typeName.Contains("PaymentCaptured"))
                {
                    var paymentId = JDoc.GetGuid(root, "PaymentId", "paymentId");
                    if (paymentId == Guid.Empty) continue;

                    var existing = await db.FactPayments.FindAsync(new object[] { paymentId }, stoppingToken);
                    if (existing != null) continue;

                    var processedAt = JDoc.GetDateTime(root, "ProcessedAt", "processedAt", "AuthorizedAt", "CapturedAt");
                    db.FactPayments.Add(new FactPayment
                    {
                        PaymentId = paymentId,
                        OrderId = JDoc.GetGuid(root, "OrderId", "orderId"),
                        Amount = JDoc.GetDecimal(root, "Amount", "amount"),
                        Currency = JDoc.GetString(root, "Currency", "currency"),
                        Method = JDoc.GetString(root, "Method", "method", "PaymentMethod", "paymentMethod"),
                        Status = "Processed",
                        ProcessedAt = processedAt,
                        DateKey = DateKeyHelper.ToDateKey(processedAt)
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] FactPayment inserted: {PaymentId}", paymentId);
                }
                else if (typeName.Contains("PaymentRefunded"))
                {
                    var paymentId = JDoc.GetGuid(root, "PaymentId", "paymentId");
                    var row = await db.FactPayments.FindAsync(new object[] { paymentId }, stoppingToken);
                    if (row != null) { row.Status = "Refunded"; await db.SaveChangesAsync(stoppingToken); }
                }
                else if (typeName.Contains("PaymentFailed"))
                {
                    var paymentId = JDoc.GetGuid(root, "PaymentId", "paymentId");
                    var row = await db.FactPayments.FindAsync(new object[] { paymentId }, stoppingToken);
                    if (row != null) { row.Status = "Failed"; await db.SaveChangesAsync(stoppingToken); }
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on payment-topic");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error on payment-topic");
            }
        }

        consumer.Close();
    }
}

// ── Inventory Consumer ──────────────────────────────────────────────────────
// Subscribes to "warehouse-topic" (WarehouseService Kafka outbox).

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
        consumer.Subscribe(new[] { "warehouse-topic" });
        _logger.LogInformation("[REPORTING] InventoryEventConsumer started on warehouse-topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;
                var typeName = JDoc.GetTypeName(root);

                // Facts belong to the tenant of the source event (stamped by the publisher's Outbox);
                // an event without one is skipped, never counted in some other tenant's reports.
                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] {EventType} without TenantId ignored", typeName);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                if (typeName.Contains("StockMoved") || typeName.Contains("InventoryMoved") ||
                    typeName.Contains("InventoryAdjusted") || typeName.Contains("ReceiptCompleted") ||
                    typeName.Contains("InventoryReserved"))
                {
                    var movementId = JDoc.GetGuid(root, "MovementId", "movementId", "Id", "id");
                    if (movementId == Guid.Empty) movementId = Guid.NewGuid();

                    var existing = await db.FactInventoryMovements.FindAsync(new object[] { movementId }, stoppingToken);
                    if (existing != null) continue;

                    var occurredAt = JDoc.GetDateTime(root, "OccurredAt", "occurredAt", "MovedAt", "movedAt", "CreatedAt");
                    db.FactInventoryMovements.Add(new FactInventoryMovement
                    {
                        MovementId = movementId,
                        ProductId = JDoc.GetGuid(root, "ProductId", "productId"),
                        MovementType = JDoc.GetString(root, "MovementType", "movementType", "Type", "type"),
                        Quantity = JDoc.GetInt(root, "Quantity", "quantity"),
                        OccurredAt = occurredAt,
                        DateKey = DateKeyHelper.ToDateKey(occurredAt)
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] FactInventoryMovement inserted: {MovementId}", movementId);
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on warehouse-topic");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error on warehouse-topic");
            }
        }

        consumer.Close();
    }
}

// ── POS Consumer ────────────────────────────────────────────────────────────
// Subscribes to "pos-topic" (POSService Kafka outbox).

public class POSEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<POSEventConsumer> _logger;

    public POSEventConsumer(IServiceProvider services, IConfiguration config, ILogger<POSEventConsumer> logger)
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
            GroupId = "reporting-pos-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(new[] { "pos-topic" });
        _logger.LogInformation("[REPORTING] POSEventConsumer started on pos-topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;
                var typeName = JDoc.GetTypeName(root);

                // Facts belong to the tenant of the source event (stamped by the publisher's Outbox);
                // an event without one is skipped, never counted in some other tenant's reports.
                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] {EventType} without TenantId ignored", typeName);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                if (typeName.Contains("TransactionCreated"))
                {
                    var txnId = JDoc.GetGuid(root, "TransactionId", "transactionId", "Id", "id");
                    if (txnId == Guid.Empty) continue;

                    var existing = await db.FactPOSTransactions.FindAsync(new object[] { txnId }, stoppingToken);
                    if (existing != null) continue;

                    var occurredAt = JDoc.GetDateTime(root, "OccurredAt", "occurredAt", "CreatedAt", "createdAt");
                    db.FactPOSTransactions.Add(new FactPOSTransaction
                    {
                        TransactionId = txnId,
                        StoreId = JDoc.GetGuid(root, "StoreId", "storeId", "TerminalId", "terminalId"),
                        CashierId = JDoc.GetGuid(root, "CashierId", "cashierId", "UserId", "userId"),
                        TotalAmount = JDoc.GetDecimal(root, "TotalAmount", "totalAmount", "Amount", "amount"),
                        TaxAmount = JDoc.GetDecimal(root, "TaxAmount", "taxAmount"),
                        ItemCount = JDoc.GetInt(root, "ItemCount", "itemCount", "LineCount", "lineCount"),
                        OccurredAt = occurredAt,
                        DateKey = DateKeyHelper.ToDateKey(occurredAt)
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] FactPOSTransaction inserted: {TxnId}", txnId);
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on pos-topic");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error on pos-topic");
            }
        }

        consumer.Close();
    }
}

// ── HRM Consumer ────────────────────────────────────────────────────────────
// Subscribes to "hrm-topic" (HRMService Kafka outbox).

public class HRMEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<HRMEventConsumer> _logger;

    public HRMEventConsumer(IServiceProvider services, IConfiguration config, ILogger<HRMEventConsumer> logger)
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
            GroupId = "reporting-hrm-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(new[] { "hrm-topic" });
        _logger.LogInformation("[REPORTING] HRMEventConsumer started on hrm-topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;
                var typeName = JDoc.GetTypeName(root);

                // Facts belong to the tenant of the source event (stamped by the publisher's Outbox);
                // an event without one is skipped, never counted in some other tenant's reports.
                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] {EventType} without TenantId ignored", typeName);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                if (typeName.Contains("PaySlipGenerated") || typeName.Contains("PayslipGenerated"))
                {
                    var payslipId = JDoc.GetGuid(root, "PaySlipId", "paySlipId", "PayslipId", "payslipId", "Id", "id");
                    if (payslipId == Guid.Empty) continue;

                    var existing = await db.FactHRPayslips.FindAsync(new object[] { payslipId }, stoppingToken);
                    if (existing != null) continue;

                    var paidAt = JDoc.GetDateTime(root, "PaidAt", "paidAt", "GeneratedAt", "generatedAt");
                    var firstName = JDoc.GetString(root, "FirstName", "firstName");
                    var lastName = JDoc.GetString(root, "LastName", "lastName");
                    var employeeName = JDoc.GetString(root, "EmployeeName", "employeeName");
                    if (string.IsNullOrEmpty(employeeName))
                        employeeName = $"{firstName} {lastName}".Trim();

                    db.FactHRPayslips.Add(new FactHRPayslip
                    {
                        PayslipId = payslipId,
                        EmployeeId = JDoc.GetGuid(root, "EmployeeId", "employeeId"),
                        EmployeeName = employeeName,
                        Period = JDoc.GetString(root, "Period", "period", "PayPeriod", "payPeriod"),
                        BasicSalary = JDoc.GetDecimal(root, "BasicSalary", "basicSalary", "BaseSalary", "baseSalary"),
                        OvertimePay = JDoc.GetDecimal(root, "OvertimePay", "overtimePay"),
                        TotalAmount = JDoc.GetDecimal(root, "TotalAmount", "totalAmount", "NetAmount", "netAmount"),
                        PaidAt = paidAt,
                        DateKey = DateKeyHelper.ToDateKey(paidAt)
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] FactHRPayslip inserted: {PayslipId}", payslipId);
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on hrm-topic");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error on hrm-topic");
            }
        }

        consumer.Close();
    }
}

// ── Product Consumer ────────────────────────────────────────────────────────
// Subscribes to "product-topic" → populates DimProduct for inventory name lookup.

public class ProductEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<ProductEventConsumer> _logger;

    public ProductEventConsumer(IServiceProvider services, IConfiguration config, ILogger<ProductEventConsumer> logger)
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
            GroupId = "reporting-product-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(new[] { "product-topic" });
        _logger.LogInformation("[REPORTING] ProductEventConsumer started on product-topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;
                var typeName = JDoc.GetTypeName(root);

                // Facts belong to the tenant of the source event (stamped by the publisher's Outbox);
                // an event without one is skipped, never counted in some other tenant's reports.
                if (!EventTenant.TryRead(result.Message.Value, out var tenantId))
                {
                    _logger.LogWarning("[REPORTING] {EventType} without TenantId ignored", typeName);
                    continue;
                }
                using var tenantScope = TenantScope.Begin(tenantId);

                if (typeName.Contains("ProductCreated") || typeName.Contains("ProductUpdated"))
                {
                    var productId = JDoc.GetGuid(root, "ProductId", "productId", "Id", "id");
                    if (productId == Guid.Empty) continue;

                    var dim = new DimProduct
                    {
                        ProductId = productId,
                        Name = JDoc.GetString(root, "Name", "name", "ProductName", "productName"),
                        Category = JDoc.GetString(root, "Category", "category", "CategoryName", "categoryName"),
                        Price = JDoc.GetDecimal(root, "Price", "price", "BasePrice", "basePrice"),
                        IsActive = true
                    };

                    var existing = await db.DimProducts.FindAsync(new object[] { productId }, stoppingToken);
                    if (existing == null)
                        await db.DimProducts.AddAsync(dim, stoppingToken);
                    else
                        db.Entry(existing).CurrentValues.SetValues(dim);

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("[REPORTING] DimProduct upserted: {ProductId}", productId);
                }
                else if (typeName.Contains("ProductDiscontinued"))
                {
                    var productId = JDoc.GetGuid(root, "ProductId", "productId", "Id", "id");
                    var row = await db.DimProducts.FindAsync(new object[] { productId }, stoppingToken);
                    if (row != null) { row.IsActive = false; await db.SaveChangesAsync(stoppingToken); }
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[REPORTING] Kafka consume error on product-topic");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPORTING] Processing error on product-topic");
            }
        }

        consumer.Close();
    }
}

// ── Registration ───────────────────────────────────────────────────────────

public static class ReportingConsumerExtensions
{
    public static IServiceCollection AddReportingKafkaConsumers(this IServiceCollection services)
    {
        services.AddHostedService<SalesOrderEventConsumer>();
        services.AddHostedService<PaymentEventConsumer>();
        services.AddHostedService<InventoryEventConsumer>();
        services.AddHostedService<POSEventConsumer>();
        services.AddHostedService<HRMEventConsumer>();
        services.AddHostedService<ProductEventConsumer>();
        return services;
    }
}
