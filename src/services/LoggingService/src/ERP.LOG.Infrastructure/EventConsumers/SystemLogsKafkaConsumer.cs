using System.Text.Json;
using Confluent.Kafka;
using ERP.LOG.Application.Interfaces;
using ERP.LOG.Domain.Models;
using ERP.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.LOG.Infrastructure.EventConsumers;

/// <summary>
/// Subscribes to the shared "system-logs-topic" (populated by every pilot service's
/// KafkaLogSink) and indexes each ServiceLogEvent into Elasticsearch via ILogQueryRepository.
/// </summary>
public class SystemLogsKafkaConsumer : BackgroundService
{
    private const string TopicName = "system-logs-topic";
    private const string GroupId = "logging-service-group";

    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<SystemLogsKafkaConsumer> _logger;

    public SystemLogsKafkaConsumer(IServiceProvider services, IConfiguration config, ILogger<SystemLogsKafkaConsumer> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _config["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(TopicName);
        _logger.LogInformation("[LOG] SystemLogsKafkaConsumer started on {Topic}", TopicName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                var logEvent = JsonSerializer.Deserialize<ServiceLogEvent>(result.Message.Value);
                if (logEvent == null) continue;

                await using var scope = _services.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<ILogQueryRepository>();

                await repository.IndexAsync(new LogEntry
                {
                    Id = logEvent.EventId.ToString(),
                    ServiceName = logEvent.ServiceName,
                    Environment = logEvent.Environment,
                    Level = logEvent.Level.ToString(),
                    Kind = logEvent.Kind.ToString(),
                    Message = logEvent.Message,
                    ExceptionType = logEvent.ExceptionType,
                    StackTrace = logEvent.StackTrace,
                    CorrelationId = logEvent.CorrelationId,
                    Source = logEvent.Source ?? "Backend",
                    Properties = logEvent.Properties,
                    OccurredAt = logEvent.OccurredAt,
                    IndexedAt = DateTime.UtcNow
                }, stoppingToken);
            }
            catch (ConsumeException ex) when (ex.Error.Code != ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogError(ex, "[LOG] Kafka consume error on {Topic}", TopicName);
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LOG] Error processing message from {Topic}", TopicName);
            }
        }

        consumer.Close();
    }
}
