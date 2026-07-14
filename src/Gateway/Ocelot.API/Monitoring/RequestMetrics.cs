using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ocelot.API.Monitoring;

/// <summary>
/// Thread-safe in-memory store for API metrics and recent exceptions.
/// Rotates logs to prevent unbounded memory growth.
/// </summary>
public sealed class RequestMetricsStore
{
    private const int MaxRecentRequests = 500;
    private const int MaxRecentExceptions = 100;

    private readonly ConcurrentQueue<ApiRequestLog> _recentRequests = new();
    private readonly ConcurrentQueue<ExceptionLog> _recentExceptions = new();
    private readonly ConcurrentDictionary<string, RouteMetrics> _routeMetrics = new();
    private long _totalRequests;
    private long _totalErrors;

    public void RecordRequest(string method, string path, int statusCode, double elapsedMs, string? tenantId, string? requestId)
    {
        Interlocked.Increment(ref _totalRequests);
        if (statusCode >= 500) Interlocked.Increment(ref _totalErrors);

        var log = new ApiRequestLog
        {
            Method = method,
            Path = path,
            StatusCode = statusCode,
            ElapsedMs = elapsedMs,
            TenantId = tenantId,
            RequestId = requestId,
            Timestamp = DateTime.UtcNow
        };

        _recentRequests.Enqueue(log);
        while (_recentRequests.Count > MaxRecentRequests)
            _recentRequests.TryDequeue(out _);

        var key = $"{method}:{NormalizePath(path)}";
        _routeMetrics.AddOrUpdate(key,
            _ => new RouteMetrics { Method = method, PathPattern = NormalizePath(path), Count = 1, TotalElapsedMs = elapsedMs, ErrorCount = statusCode >= 400 ? 1 : 0 },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalElapsedMs += elapsedMs;
                if (statusCode >= 400) existing.ErrorCount++;
                if (statusCode >= 500) existing.ServerErrorCount++;
                return existing;
            });
    }

    public void RecordException(Exception ex, string? path, string? method, string? requestId)
    {
        var log = new ExceptionLog
        {
            ExceptionType = ex.GetType().Name,
            Message = ex.Message,
            StackTrace = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim(),
            Path = path,
            Method = method,
            RequestId = requestId,
            Timestamp = DateTime.UtcNow
        };

        _recentExceptions.Enqueue(log);
        while (_recentExceptions.Count > MaxRecentExceptions)
            _recentExceptions.TryDequeue(out _);
    }

    public ApiMetricsSummary GetSummary() => new()
    {
        TotalRequests = Interlocked.Read(ref _totalRequests),
        TotalErrors = Interlocked.Read(ref _totalErrors),
        RecentRequests = _recentRequests.OrderByDescending(r => r.Timestamp).Take(50).ToList(),
        RecentExceptions = _recentExceptions.OrderByDescending(e => e.Timestamp).ToList(),
        TopRoutes = _routeMetrics.Values
            .OrderByDescending(r => r.Count)
            .Take(20)
            .Select(r => new RouteStatDto
            {
                Method = r.Method,
                PathPattern = r.PathPattern,
                Count = r.Count,
                AvgElapsedMs = r.Count > 0 ? Math.Round(r.TotalElapsedMs / r.Count, 1) : 0,
                ErrorCount = r.ErrorCount,
                ErrorRate = r.Count > 0 ? Math.Round((double)r.ErrorCount / r.Count * 100, 1) : 0
            })
            .ToList()
    };

    private static string NormalizePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return "/" + string.Join("/", segments.Select(s =>
            Guid.TryParse(s, out _) || long.TryParse(s, out _) ? "{id}" : s));
    }
}

public record ApiRequestLog
{
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public double ElapsedMs { get; init; }
    public string? TenantId { get; init; }
    /// <summary>HttpContext.TraceIdentifier — matches the "RequestId" property Serilog's
    /// request-logging middleware stamps on every log line for this request, so the Admin UI
    /// can deep-link from a Monitoring row to the exact matching entries in System Logs.</summary>
    public string? RequestId { get; init; }
    public DateTime Timestamp { get; init; }
}

public record ExceptionLog
{
    public string ExceptionType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? StackTrace { get; init; }
    public string? Path { get; init; }
    public string? Method { get; init; }
    public string? RequestId { get; init; }
    public DateTime Timestamp { get; init; }
}

public class RouteMetrics
{
    public string Method { get; set; } = string.Empty;
    public string PathPattern { get; set; } = string.Empty;
    public long Count { get; set; }
    public double TotalElapsedMs { get; set; }
    public long ErrorCount { get; set; }
    public long ServerErrorCount { get; set; }
}

public record RouteStatDto
{
    public string Method { get; init; } = string.Empty;
    public string PathPattern { get; init; } = string.Empty;
    public long Count { get; init; }
    public double AvgElapsedMs { get; init; }
    public long ErrorCount { get; init; }
    public double ErrorRate { get; init; }
}

public record ApiMetricsSummary
{
    public long TotalRequests { get; init; }
    public long TotalErrors { get; init; }
    public List<ApiRequestLog> RecentRequests { get; init; } = [];
    public List<ExceptionLog> RecentExceptions { get; init; } = [];
    public List<RouteStatDto> TopRoutes { get; init; } = [];
}
