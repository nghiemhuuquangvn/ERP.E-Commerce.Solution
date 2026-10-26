using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Infrastructure.EventBus;

/// <summary>
/// Consumes PurchaseOrderService's "purchasing-topic" (Outbox pattern, Newtonsoft
/// TypeNameHandling.All — dispatch by "$type" substring, mirrors
/// SaleOrderService.ProductPriceChangedConsumer). PurchaseOrderReceivedDomainEvent
/// only carries ProductCode (string), not the WarehouseService ProductId (Guid), so
/// lines are matched by Inventory.ProductSku; a line with no matching Inventory row
/// is logged and skipped rather than fabricating a new Inventory keyed by a random
/// Guid that would never correlate with the real product in ProductService.
/// </summary>
public class PurchaseOrderReceivedConsumer : IHostedService
{
    private const string PurchasingTopic = "purchasing-topic";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PurchaseOrderReceivedConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public PurchaseOrderReceivedConsumer(IServiceProvider serviceProvider, ILogger<PurchaseOrderReceivedConsumer> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "warehouse-service-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(PurchasingTopic);
        _executingTask = Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;

        _cancellationTokenSource?.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));

        _consumer.Close();
        _consumer.Dispose();
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(5));
                if (result?.Message?.Value == null)
                    continue;

                await DispatchAsync(result.Message.Value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming from purchasing-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from purchasing-topic");
            }
        }
    }

    private const int MaxAttempts = 5;

    /// <summary>
    /// Goods sent back to a supplier (ROADMAP P1-8): the stock is issued. A return the stock cannot cover fails as a whole; it is tried up to 5 times
    /// (stock may arrive meanwhile) and then logged with its payload, never retried for ever.
    /// </summary>
    private async Task IssueVendorReturnAsync(JObject raw, string json, CancellationToken cancellationToken)
    {
        var returnId = raw.GuidValue("VendorReturnId") ?? Guid.Empty;
        var lines = raw.ArrayValue("Lines");
        if (returnId == Guid.Empty || lines == null)
            return;

        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("VendorReturnShipped {ReturnId} without TenantId ignored", returnId);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var tenantScope = TenantScope.Begin(tenantId);
                using var scope = _serviceProvider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new IssueVendorReturnCommand
                {
                    VendorReturnId = returnId,
                    ReturnNumber = raw.Value<string>("ReturnNumber") ?? string.Empty,
                    PurchaseOrderId = raw.GuidValue("PurchaseOrderId") ?? Guid.Empty,
                    Lines = lines.Select(l => new VendorReturnLine
                    {
                        ProductCode = l.Value<string>("ProductCode") ?? string.Empty,
                        Quantity = (int)(l.Value<decimal?>("Quantity") ?? 0m)
                    }).ToList()
                }, cancellationToken);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} of {Max} failed for vendor return {ReturnId}; retrying", attempt, MaxAttempts, returnId);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Giving up on vendor return {ReturnId} after {Max} attempts; the stock was NOT issued. Payload: {Payload}", returnId, MaxAttempts, json);
                return;
            }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var raw = JObject.Parse(json);
        var typeName = raw["$type"]?.ToString() ?? string.Empty;
        if (typeName.Contains("VendorReturnShippedDomainEvent"))
        {
            await IssueVendorReturnAsync(raw, json, cancellationToken);
            return;
        }
        if (!typeName.Contains("PurchaseOrderReceivedDomainEvent"))
            return; // Other purchasing-topic events are not yet consumed by WarehouseService.

        var purchaseOrderId = raw.GuidValue("PurchaseOrderId") ?? Guid.Empty;
        var receivedItems = raw.ArrayValue("ReceivedItems");
        if (purchaseOrderId == Guid.Empty || receivedItems == null)
            return;

        // Goods are received into the purchase order's tenant (stamped by PurchaseOrderService).
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("PurchaseOrderReceived {PurchaseOrderId} without TenantId ignored", purchaseOrderId);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var inventoryRepository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();

        foreach (var item in receivedItems)
        {
            var productCode = item.Value<string>("ProductCode");
            var quantityReceived = item.Value<decimal?>("QuantityReceived") ?? 0m;
            var receiptId = item.GuidValue("GoodsReceiptId");
            if (string.IsNullOrWhiteSpace(productCode) || quantityReceived <= 0)
                continue;

            var inventory = await inventoryRepository.GetByProductSkuAsync(productCode, cancellationToken);
            if (inventory == null)
            {
                _logger.LogWarning(
                    "Received PO {PurchaseOrderId} line for product code {ProductCode} has no matching Inventory (by SKU) — skipping. " +
                    "Inventory rows must exist (e.g. via manual receiving) before PO-driven receipts can be matched by code.",
                    purchaseOrderId, productCode);
                continue;
            }

            await mediator.Send(new ReceiveStockCommand
            {
                ProductId = inventory.ProductId,
                Quantity = (int)quantityReceived,
                UnitCost = item.Value<decimal?>("UnitPrice") ?? 0m,
                // One receipt (GRN) per delivery: a second partial receipt of the same product on the same order is new stock,
                // not a redelivery. Events from before the GRN id existed keep the old per-order reference.
                Reference = receiptId.HasValue ? $"PO-{purchaseOrderId}-{receiptId.Value:N}" : $"PO-{purchaseOrderId}",
                PurchaseOrderId = purchaseOrderId
            }, cancellationToken);
        }
    }
}
