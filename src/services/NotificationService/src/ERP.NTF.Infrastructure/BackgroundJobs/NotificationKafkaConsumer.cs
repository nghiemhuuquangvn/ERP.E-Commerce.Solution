using Confluent.Kafka;
using ERP.NTF.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.NTF.Infrastructure.BackgroundJobs;

/// <summary>
/// Consumes payment-topic (PaymentProcessed), shipping-topic (ShipmentCreated),
/// and mst-topic (EmployeeHired) from Kafka. All three topics publish via the Outbox
/// pattern with Newtonsoft TypeNameHandling.All ($type discriminator). Follows
/// IdentityEventConsumer's dispatch pattern — mediator.Publish so existing
/// INotificationHandler implementations in IntegrationEventHandlers.cs are triggered.
/// </summary>
public class NotificationKafkaConsumer : IHostedService
{
    private const string PaymentTopic = "payment-topic";
    private const string ShippingTopic = "shipping-topic";
    private const string MstTopic = "mst-topic";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationKafkaConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cts;
    private Task? _executingTask;

    public NotificationKafkaConsumer(
        IServiceProvider serviceProvider,
        ILogger<NotificationKafkaConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "notification-service-events-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(new[] { PaymentTopic, ShippingTopic, MstTopic });
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

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(5));
                if (result?.Message?.Value == null) continue;

                await DispatchAsync(result.Message.Value, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Kafka consume error"); }
            catch (Exception ex) { _logger.LogError(ex, "Error handling Kafka message"); }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        if (typeName.Contains("PaymentProcessedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PaymentProcessedIntegrationEvent
            {
                PaymentId = raw.Value<Guid?>("PaymentId") ?? Guid.Empty,
                OrderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty,
                RecipientEmail = raw.Value<string>("CustomerEmail") ?? raw.Value<string>("Email") ?? string.Empty,
                RecipientName = raw.Value<string>("CustomerName") ?? string.Empty,
                Amount = raw.Value<decimal?>("Amount") ?? 0m,
                Currency = raw.Value<string>("Currency") ?? "VND"
            };
            if (evt.PaymentId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("ShipmentCreated") || typeName.Contains("ShipmentCreatedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new ShipmentCreatedIntegrationEvent
            {
                ShipmentId = raw.Value<Guid?>("ShipmentId") ?? Guid.Empty,
                OrderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty,
                CustomerEmail = raw.Value<string>("CustomerEmail") ?? string.Empty,
                CustomerName = raw.Value<string>("CustomerName") ?? string.Empty,
                TrackingNumber = raw.Value<string>("TrackingNumber") ?? string.Empty,
                CarrierName = raw.Value<string>("CarrierName") ?? raw.Value<string>("Carrier") ?? string.Empty
            };
            if (evt.ShipmentId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("EmployeeHired"))
        {
            var raw = JObject.Parse(json);
            var evt = new EmployeeHiredIntegrationEvent
            {
                EmployeeId = raw.Value<Guid?>("EmployeeId") ?? Guid.Empty,
                Email = raw.Value<string>("Email") ?? string.Empty,
                FirstName = raw.Value<string>("FirstName") ?? string.Empty,
                LastName = raw.Value<string>("LastName") ?? string.Empty,
                Department = raw.Value<string>("Department") ?? raw.Value<string>("DepartmentName") ?? string.Empty
            };
            if (evt.EmployeeId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
    }
}
