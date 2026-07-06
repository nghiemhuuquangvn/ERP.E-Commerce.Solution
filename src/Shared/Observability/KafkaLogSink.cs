using System.Text.Json;
using Confluent.Kafka;
using ERP.Shared.Contracts;
using Serilog.Core;
using Serilog.Events;

namespace ERP.Shared.Observability
{
    /// <summary>
    /// Serilog sink that ships every log event to the "system-logs-topic" Kafka topic for
    /// LoggingService to consume and index into Elasticsearch. Must never throw or block the
    /// logging pipeline — Kafka unavailability degrades to a Console.Error line, not a crash.
    /// </summary>
    public sealed class KafkaLogSink : ILogEventSink, IDisposable
    {
        public const string TopicName = "system-logs-topic";

        private readonly IProducer<string, string> _producer;
        private readonly string _serviceName;
        private readonly string _environment;

        public KafkaLogSink(string bootstrapServers, string serviceName, string environment)
        {
            _serviceName = serviceName;
            _environment = environment;

            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.Leader,
                MessageTimeoutMs = 5000,
                MessageSendMaxRetries = 1
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public void Emit(LogEvent logEvent)
        {
            try
            {
                var hasException = logEvent.Exception != null;

                var entry = new ServiceLogEvent
                {
                    ServiceName = _serviceName,
                    Environment = _environment,
                    Level = MapLevel(logEvent.Level),
                    Kind = hasException ? LogKind.Exception : LogKind.Trace,
                    Message = logEvent.RenderMessage(),
                    ExceptionType = hasException ? logEvent.Exception!.GetType().FullName : null,
                    StackTrace = hasException ? logEvent.Exception!.ToString() : null,
                    Source = "Backend",
                    Properties = logEvent.Properties.Count > 0
                        ? logEvent.Properties.ToDictionary(p => p.Key, p => p.Value.ToString())
                        : null
                };

                var payload = JsonSerializer.Serialize(entry);

                _producer.Produce(
                    TopicName,
                    new Message<string, string> { Key = entry.EventId.ToString(), Value = payload },
                    deliveryReport =>
                    {
                        if (deliveryReport.Error.IsError)
                        {
                            Console.Error.WriteLine(
                                $"[KafkaLogSink] Failed to deliver log to {TopicName}: {deliveryReport.Error.Reason}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[KafkaLogSink] Error emitting log event: {ex.Message}");
            }
        }

        private static LogSeverity MapLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Warning => LogSeverity.Warning,
            LogEventLevel.Error => LogSeverity.Error,
            LogEventLevel.Fatal => LogSeverity.Critical,
            _ => LogSeverity.Information
        };

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(2));
            _producer.Dispose();
        }
    }
}
