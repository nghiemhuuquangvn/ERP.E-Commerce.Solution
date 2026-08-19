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
/// Example Kafka consumer, replacing the source monorepo's three ERP-specific
/// consumers (IdentityService's identity-topic, a payment/shipping/master-data
/// consumer, and a RabbitMQ sales-order consumer) — those assumed sibling services
/// that don't exist in a standalone deployment.
///
/// Subscribes to a single placeholder topic and dispatches ExampleIntegrationEvent
/// via MediatR. Replace "example-topic" and the field mapping in DispatchAsync with
/// your own upstream service's topic name and event shape; add one IHostedService
/// like this per topic you need to consume, and register it in Program.cs.
/// </summary>
public class ExampleKafkaConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExampleKafkaConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public ExampleKafkaConsumer(
        IServiceProvider serviceProvider,
        ILogger<ExampleKafkaConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "notification-service-example-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe("example-topic");
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
                _logger.LogError(ex, "Error consuming from example-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from example-topic");
            }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var raw = JObject.Parse(json);
        var evt = new ExampleIntegrationEvent
        {
            RecipientId = raw.Value<Guid?>("RecipientId") ?? Guid.Empty,
            RecipientEmail = raw.Value<string>("RecipientEmail") ?? string.Empty,
            RecipientName = raw.Value<string>("RecipientName") ?? string.Empty
        };

        if (evt.RecipientId != Guid.Empty)
            await mediator.Publish(evt, cancellationToken);
    }
}
