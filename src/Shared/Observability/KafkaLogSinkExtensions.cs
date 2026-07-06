using Serilog;
using Serilog.Configuration;

namespace ERP.Shared.Observability
{
    public static class KafkaLogSinkExtensions
    {
        /// <summary>
        /// Ships every log event (including unhandled exceptions logged via <see cref="GlobalExceptionMiddleware"/>-
        /// style handling) to the shared "system-logs-topic" Kafka topic consumed by LoggingService.
        /// </summary>
        public static LoggerConfiguration KafkaLog(
            this LoggerSinkConfiguration sinkConfiguration,
            string bootstrapServers,
            string serviceName,
            string environment)
        {
            return sinkConfiguration.Sink(new KafkaLogSink(bootstrapServers, serviceName, environment));
        }
    }
}
