namespace ERP.LOG.Domain.Models;

/// <summary>
/// Elasticsearch document shape for a single log entry, stored in the monthly rolling
/// "service-logs-{yyyy.MM}" index family. Mirrors ERP.Shared.Contracts.ServiceLogEvent —
/// LoggingService is the only place that ever writes this document (from the Kafka consumer
/// for backend services, or directly from the client-ingestion HTTP endpoint for frontend apps).
/// </summary>
public class LogEntry
{
    /// <summary>Elasticsearch document _id (the originating event's EventId).</summary>
    public string Id { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string? Environment { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ExceptionType { get; set; }

    public string? StackTrace { get; set; }

    public string? CorrelationId { get; set; }

    public string? Source { get; set; }

    public Dictionary<string, string>? Properties { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime IndexedAt { get; set; }
}
