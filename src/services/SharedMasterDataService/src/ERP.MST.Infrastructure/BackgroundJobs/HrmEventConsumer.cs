using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
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
/// Consumes HRMService's "hrm-topic" for LeaveApprovedEvent only, to keep the
/// canonical Employee's status in sync when HRM approves leave (HRM no longer owns
/// Employee, so it can't flip the real aggregate's status itself).
/// </summary>
public class HrmEventConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HrmEventConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public HrmEventConsumer(
        IServiceProvider serviceProvider,
        ILogger<HrmEventConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "masterdata-service-hrm-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe("hrm-topic");
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
                _logger.LogError(ex, "Error consuming from hrm-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from hrm-topic");
            }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

        // The event carries its tenant (Identity: the user's tenant; HRM: the leave request's tenant); without one nothing is
        // changed (fail closed).
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("{EventType} without TenantId ignored", typeName);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);
        if (!typeName.Contains("LeaveApprovedEvent"))
            return; // Other hrm-topic events (LeaveRejected/PaySlipGenerated) aren't consumed here.

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<LeaveApprovedEventHandler>();

        var evt = JsonConvert.DeserializeObject<LeaveApprovedIntegrationEvent>(json);
        if (evt != null) await handler.HandleAsync(evt, cancellationToken);
    }
}
