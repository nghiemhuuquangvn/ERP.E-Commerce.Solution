using System.Diagnostics;

namespace Ocelot.API.Monitoring;

public sealed class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestMetricsStore _metrics;
    private readonly ILogger<RequestMetricsMiddleware> _logger;

    public RequestMetricsMiddleware(RequestDelegate next, RequestMetricsStore metrics, ILogger<RequestMetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;
        var requestId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordException(ex, path, method, requestId);
            _metrics.RecordRequest(method, path, 500, sw.Elapsed.TotalMilliseconds,
                context.Request.Headers["X-Tenant-Id"].FirstOrDefault(), requestId);
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", method, path);
            throw;
        }

        sw.Stop();
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        _metrics.RecordRequest(method, path, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds, tenantId, requestId);

        if (context.Response.StatusCode >= 400)
        {
            _logger.LogWarning("HTTP {StatusCode} — {Method} {Path} [{Elapsed}ms] TenantId={TenantId}",
                context.Response.StatusCode, method, path,
                Math.Round(sw.Elapsed.TotalMilliseconds), tenantId ?? "none");
        }
    }
}
