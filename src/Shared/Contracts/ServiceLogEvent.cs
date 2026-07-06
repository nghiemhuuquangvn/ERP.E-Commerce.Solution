namespace ERP.Shared.Contracts
{
    /// <summary>
    /// Severity of a <see cref="ServiceLogEvent"/>, mirroring Serilog's <c>LogEventLevel</c>
    /// (Verbose/Debug collapse into Information for this feature's purposes).
    /// </summary>
    public enum LogSeverity
    {
        Information,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Distinguishes a plain trace/message log from one carrying exception details,
    /// mirroring Azure Application Insights' Traces vs Exceptions telemetry types.
    /// </summary>
    public enum LogKind
    {
        Trace,
        Exception
    }

    /// <summary>
    /// Wire contract for the centralized log management feature. Published by backend services
    /// to the "system-logs-topic" Kafka topic (via the shared Serilog Kafka sink) and posted
    /// directly by frontend apps to LoggingService's client-ingestion endpoint. LoggingService
    /// is the sole subscriber and indexes these into Elasticsearch.
    /// </summary>
    public class ServiceLogEvent : IntegrationEventBase
    {
        public string ServiceName { get; set; } = string.Empty;

        public string? Environment { get; set; }

        public LogSeverity Level { get; set; }

        public LogKind Kind { get; set; }

        public string Message { get; set; } = string.Empty;

        /// <summary>Set only when <see cref="Kind"/> is <see cref="LogKind.Exception"/>.</summary>
        public string? ExceptionType { get; set; }

        /// <summary>Set only when <see cref="Kind"/> is <see cref="LogKind.Exception"/>.</summary>
        public string? StackTrace { get; set; }

        public string? CorrelationId { get; set; }

        /// <summary>"Backend" or "Frontend" — distinguishes Kafka-sourced from HTTP-ingested entries.</summary>
        public string? Source { get; set; }

        /// <summary>Structured extras: request path/method/statusCode, userId, client url/userAgent, etc.</summary>
        public Dictionary<string, string>? Properties { get; set; }
    }
}
