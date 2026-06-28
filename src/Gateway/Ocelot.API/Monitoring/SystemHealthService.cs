using System.Diagnostics;
using System.Runtime;

namespace Ocelot.API.Monitoring;

public sealed class SystemHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    // Downstream service URLs for health probe
    private static readonly Dictionary<string, string> ServiceHealthUrls = new()
    {
        ["IdentityService"]     = "http://localhost:5001/health",
        ["SaleOrderService"]    = "http://localhost:5002/health",
        ["PurchaseOrderService"]= "http://localhost:5003/health",
        ["WareHouseService"]    = "http://localhost:5004/health",
        ["AccountingService"]   = "http://localhost:5005/health",
        ["CRMService"]          = "http://localhost:5006/health",
        ["HRMService"]          = "http://localhost:5007/health",
        ["ProductService"]      = "http://localhost:5008/health",
        ["PaymentService"]      = "http://localhost:5009/health",
        ["ShippingService"]     = "http://localhost:5010/health",
        ["POSService"]          = "http://localhost:5011/health",
        ["PromotionService"]    = "http://localhost:5012/health",
        ["NotificationService"] = "http://localhost:5013/health",
        ["ReportingService"]    = "http://localhost:5014/health",
    };

    public SystemHealthService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public ProcessMetricsDto GetProcessMetrics()
    {
        var process = Process.GetCurrentProcess();
        return new ProcessMetricsDto
        {
            MachineName = Environment.MachineName,
            ProcessName = process.ProcessName,
            WorkingSetMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            StartTime = process.StartTime.ToUniversalTime(),
            UptimeSeconds = (long)(DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
            GcGen0Collections = GC.CollectionCount(0),
            GcGen1Collections = GC.CollectionCount(1),
            GcGen2Collections = GC.CollectionCount(2),
            TotalMemoryMb = Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 1),
            DotNetVersion = Environment.Version.ToString()
        };
    }

    public async Task<List<ServiceHealthDto>> GetServicesHealthAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("health");
        var tasks = ServiceHealthUrls.Select(async kv =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));
                var response = await client.GetAsync(kv.Value, cts.Token);
                sw.Stop();
                return new ServiceHealthDto
                {
                    ServiceName = kv.Key,
                    Status = response.IsSuccessStatusCode ? "Healthy" : "Degraded",
                    ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
                    CheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ServiceHealthDto
                {
                    ServiceName = kv.Key,
                    Status = "Unreachable",
                    ErrorMessage = ex.Message,
                    ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
                    CheckedAt = DateTime.UtcNow
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}

public record ProcessMetricsDto
{
    public string MachineName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public double WorkingSetMb { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public DateTime StartTime { get; init; }
    public long UptimeSeconds { get; init; }
    public int GcGen0Collections { get; init; }
    public int GcGen1Collections { get; init; }
    public int GcGen2Collections { get; init; }
    public double TotalMemoryMb { get; init; }
    public string DotNetVersion { get; init; } = string.Empty;
}

public record ServiceHealthDto
{
    public string ServiceName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CheckedAt { get; init; }
}
