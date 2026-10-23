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
/// Consumes POSService's "pos-topic" (Outbox pattern, Newtonsoft TypeNameHandling.All —
/// dispatch by "$type" substring, mirrors PurchaseOrderReceivedConsumer).
/// TransactionCreatedEvent takes the sold lines out of stock, TransactionVoidedEvent puts
/// them back. Before this consumer existed POS sales never reduced inventory.
/// </summary>
public class PosTransactionConsumer : IHostedService
{
    private const string PosTopic = "pos-topic";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PosTransactionConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public PosTransactionConsumer(IServiceProvider serviceProvider, ILogger<PosTransactionConsumer> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "warehouse-service-pos-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(PosTopic);
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
                _logger.LogError(ex, "Error consuming from pos-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from pos-topic");
            }
        }
    }

    /// <summary>Maps a pos-topic payload to the stock command, or null when it isn't a stock-relevant event.</summary>
    public static RecordPointOfSaleStockCommand? ToCommand(JObject raw)
    {
        var typeName = raw["$type"]?.ToString() ?? string.Empty;
        bool isReturn;
        if (typeName.Contains("TransactionCreatedEvent"))
            isReturn = false;
        else if (typeName.Contains("TransactionVoidedEvent"))
            isReturn = true;
        else
            return null; // CashShiftClosed etc. don't move stock.

        var transactionId = raw.GuidValue("TransactionId") ?? Guid.Empty;
        if (transactionId == Guid.Empty)
            return null;

        var lines = raw.ArrayValue("Lines")?.Select(l => new PointOfSaleLine
        {
            ProductId = l.GuidValue("ProductId") ?? Guid.Empty,
            ProductName = l.Value<string?>("ProductName") ?? string.Empty,
            Quantity = l.Value<int?>("Quantity") ?? 0
        }).ToList() ?? new List<PointOfSaleLine>();

        return new RecordPointOfSaleStockCommand { TransactionId = transactionId, IsReturn = isReturn, Lines = lines };
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var command = ToCommand(JObject.Parse(json));
        if (command == null)
            return;

        if (command.Lines.Count == 0)
        {
            // Published by a POSService older than the Lines field — nothing to apply.
            _logger.LogWarning("POS transaction {TransactionId} event has no lines — stock not changed", command.TransactionId);
            return;
        }

        // Stock belongs to the tenant of the till that sold it (stamped by POSService).
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("POS transaction {TransactionId} without TenantId ignored", command.TransactionId);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, cancellationToken);
    }
}
