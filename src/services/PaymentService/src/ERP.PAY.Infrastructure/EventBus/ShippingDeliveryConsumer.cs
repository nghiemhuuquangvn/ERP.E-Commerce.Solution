using Confluent.Kafka;
using ERP.PAY.Application.Handlers;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.PAY.Infrastructure.EventBus;

/// <summary>
/// Consumes ShippingService's "shipping-topic" for DeliveryConfirmedEvent: a cash-on-delivery order is paid when the goods arrive, so
/// the cash of the delivered parcel is captured now (one PaymentProcessed per parcel, and Accounting books each part). Orders that are not COD
/// are left alone; a redelivered event changes nothing. A failing message is retried 5 times in process, then logged.
/// </summary>
public class ShippingDeliveryConsumer : IHostedService
{
    private const string ShippingTopic = "shipping-topic";
    private const int MaxAttempts = 5;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShippingDeliveryConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cts;
    private Task? _executingTask;

    public ShippingDeliveryConsumer(IServiceProvider serviceProvider, ILogger<ShippingDeliveryConsumer> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "payment-service-delivery-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        }).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(ShippingTopic);
        _executingTask = Task.Run(() => ExecuteAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;
        _cts?.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        _consumer.Close();
        _consumer.Dispose();
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(5));
                if (result?.Message?.Value == null) continue;
                await HandleWithRetriesAsync(result.Message.Value, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Error consuming from {Topic}", ShippingTopic); }
        }
    }

    private async Task HandleWithRetriesAsync(string json, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DispatchAsync(json, ct);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Delivery message failed (attempt {Attempt} of {Max})", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Delivery message dropped after {Max} attempts: {Payload}", MaxAttempts, json);
                return;
            }
        }
    }

    internal async Task DispatchAsync(string json, CancellationToken ct)
    {
        var raw = JObject.Parse(json);
        if (!(raw["$type"]?.ToString() ?? string.Empty).Contains("DeliveryConfirmedEvent")) return;

        var orderId = raw.GuidValue("OrderId") ?? Guid.Empty;
        if (orderId == Guid.Empty) return;
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("DeliveryConfirmed for order {OrderId} has no TenantId — ignored", orderId);
            return;
        }

        using var tenantScope = TenantScope.Begin(tenantId);
        using var scope = _serviceProvider.CreateScope();
        var collected = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CollectCashOnDeliveryCommand
        {
            OrderId = orderId,
            // Cash of an order in several parcels is collected parcel by parcel (ROADMAP R43).
            ShipmentId = raw.GuidValue("ShipmentId") ?? Guid.Empty,
            Amount = raw["CodAmount"]?.Value<decimal>() ?? 0m,
            OtherParcelsPending = raw["OtherParcelsPending"]?.Value<int>() ?? 0,
        }, ct);
        if (collected) _logger.LogInformation("Cash on delivery collected for a parcel of order {OrderId}", orderId);
    }
}
