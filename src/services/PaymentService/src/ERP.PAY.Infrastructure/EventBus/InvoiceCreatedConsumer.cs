using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using Confluent.Kafka;
using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.PAY.Infrastructure.EventBus;

/// <summary>
/// Consumes AccountingService's "accounting-topic" (Outbox pattern, Newtonsoft
/// TypeNameHandling.All — dispatch by "$type" substring, mirrors
/// SaleOrderService.ProductPriceChangedConsumer) for InvoiceCreatedEvent, so
/// invoice-based payments (net-terms/B2B orders that don't go through the immediate
/// checkout-triggered InitiatePaymentCommand) get a Payment record too. Since
/// Payment.OrderId is unique, an order that already has a payment (from the
/// SalesOrderCreated flow) is linked to the invoice instead of creating a second one.
/// </summary>
public class InvoiceCreatedConsumer : IHostedService
{
    private const string AccountingTopic = "accounting-topic";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InvoiceCreatedConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public InvoiceCreatedConsumer(IServiceProvider serviceProvider, ILogger<InvoiceCreatedConsumer> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "payment-service-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(AccountingTopic);
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
                _logger.LogError(ex, "Error consuming from accounting-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from accounting-topic");
            }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var raw = JObject.Parse(json);
        var typeName = raw["$type"]?.ToString() ?? string.Empty;
        if (!typeName.Contains("InvoiceCreatedEvent"))
            return; // Other accounting-topic events are not yet consumed by PaymentService.

        var invoiceId = raw.Value<Guid?>("InvoiceId") ?? Guid.Empty;
        var orderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty;
        var amount = raw.Value<decimal?>("Amount") ?? 0m;
        if (invoiceId == Guid.Empty || orderId == Guid.Empty || amount <= 0)
            return;

        // The invoice's tenant (stamped by AccountingService's Outbox) — never guessed.
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("InvoiceCreated {InvoiceId} has no TenantId — ignored", invoiceId);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        var existing = await payments.GetByOrderIdAsync(orderId, cancellationToken);
        if (existing != null)
        {
            await mediator.Send(new LinkInvoiceCommand { PaymentId = existing.Id, InvoiceId = invoiceId }, cancellationToken);
            return;
        }

        await mediator.Send(new InitiatePaymentCommand
        {
            OrderId = orderId,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = "VND",
            Method = PaymentMethod.BankTransfer
        }, cancellationToken);
    }
}
