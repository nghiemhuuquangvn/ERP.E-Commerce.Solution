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

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var raw = JObject.Parse(json);
        var typeName = raw["$type"]?.ToString() ?? string.Empty;
        if (!typeName.Contains("PurchaseOrderReceivedDomainEvent"))
            return; // Other purchasing-topic events are not yet consumed by WarehouseService.

        var purchaseOrderId = raw.Value<Guid?>("PurchaseOrderId") ?? Guid.Empty;
        var receivedItems = raw["ReceivedItems"] as JArray;
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
                UnitCost = 0,
                Reference = $"PO-{purchaseOrderId}",
                PurchaseOrderId = purchaseOrderId
            }, cancellationToken);
        }
    }
}
