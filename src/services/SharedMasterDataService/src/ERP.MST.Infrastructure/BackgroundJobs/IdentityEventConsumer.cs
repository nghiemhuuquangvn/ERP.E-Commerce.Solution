using Confluent.Kafka;
using ERP.MST.Application.EventHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.MST.Infrastructure.BackgroundJobs;

/// <summary>
/// Consumes IdentityService's "identity-topic" for UserRegisteredDomainEvent.
/// When a user is registered with IsEmployee=true and a DefaultDepartmentId,
/// the UserCreatedEventHandler automatically creates an Employee record in MasterDataDB.
/// Follows HrmEventConsumer's pattern (direct handler dispatch, not MediatR publish).
/// </summary>
public class IdentityEventConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdentityEventConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public IdentityEventConsumer(
        IServiceProvider serviceProvider,
        ILogger<IdentityEventConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "masterdata-service-identity-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe("identity-topic");
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
                if (result?.Message?.Value == null) continue;

                await DispatchAsync(result.Message.Value, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Error consuming from identity-topic"); }
            catch (Exception ex) { _logger.LogError(ex, "Error handling message from identity-topic"); }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;
        if (!typeName.Contains("UserRegisteredDomainEvent"))
            return;

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UserCreatedEventHandler>();

        // IdentityService's UserRegisteredDomainEvent carries UserId, TenantId, Email,
        // UserType, OccurredOn. IsEmployee and DefaultDepartmentId are not in that event
        // — we default IsEmployee=false so the handler's guard skips auto-creation unless
        // the event explicitly carries those fields in a future evolution.
        var raw = JObject.Parse(json);
        var evt = new UserRegisteredIntegrationEvent
        {
            UserId = raw.Value<Guid?>("UserId") ?? Guid.Empty,
            Email = raw.Value<string>("Email") ?? string.Empty,
            Name = raw.Value<string>("Name") ?? raw.Value<string>("Email") ?? string.Empty,
            IsEmployee = raw.Value<bool?>("IsEmployee") ?? false,
            DefaultDepartmentId = raw.Value<Guid?>("DefaultDepartmentId")
        };

        if (evt.UserId != Guid.Empty)
            await handler.HandleAsync(evt, cancellationToken);
    }
}
