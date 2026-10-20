using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Confluent.Kafka;
using ERP.HRM.Application.EventHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.HRM.Infrastructure.BackgroundJobs;

/// <summary>
/// Consumes SharedMasterDataService's "mst-topic" to keep the local EmployeeSnapshot
/// projection in sync — HRM no longer owns Employee, but LeaveRequest/PaySlip handlers
/// still need to check employee status/enumerate active employees locally, without a
/// synchronous cross-service call (this codebase has none; every cross-service
/// relationship goes through Kafka + the Outbox pattern).
/// </summary>
public class MasterDataEventConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MasterDataEventConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public MasterDataEventConsumer(
        IServiceProvider serviceProvider,
        ILogger<MasterDataEventConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "hrm-service-masterdata-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe("mst-topic");
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
                _logger.LogError(ex, "Error consuming from mst-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from mst-topic");
            }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        // KafkaMessageBus (producer side, in every service) serializes with
        // Newtonsoft TypeNameHandling.All, which embeds a "$type" discriminator
        // holding the publisher's fully-qualified CLR type name — used here only to
        // pick which local contract shape to deserialize into.
        var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

        // Employee events carry the employee's tenant (stamped by SharedMasterDataService); without one nothing is
        // changed (fail closed).
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("{EventType} without TenantId ignored", typeName);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<EmployeeMasterDataEventHandlers>();

        if (typeName.Contains("EmployeeHiredEvent"))
        {
            var evt = JsonConvert.DeserializeObject<EmployeeHiredIntegrationEvent>(json);
            if (evt != null) await handler.HandleAsync(evt, cancellationToken);
        }
        else if (typeName.Contains("EmployeeTerminatedEvent"))
        {
            var evt = JsonConvert.DeserializeObject<EmployeeTerminatedIntegrationEvent>(json);
            if (evt != null) await handler.HandleAsync(evt, cancellationToken);
        }
        else if (typeName.Contains("EmployeeTransferredEvent"))
        {
            var evt = JsonConvert.DeserializeObject<EmployeeTransferredIntegrationEvent>(json);
            if (evt != null) await handler.HandleAsync(evt, cancellationToken);
        }
        // Other mst-topic events (Department/Position/UnitOfMeasure/Geography) are not
        // consumed by HRM — no local read-model needs them today.
    }
}
