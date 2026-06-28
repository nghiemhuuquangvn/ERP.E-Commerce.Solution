using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ocelot.API.Monitoring;
using System.Security.Claims;

namespace Ocelot.API.Controllers;

/// <summary>
/// System monitoring endpoints — restricted to users with 'monitoring:view' permission.
/// Provides: process metrics, service health, API call stats, recent exceptions.
/// </summary>
[ApiController]
[Route("api/v1/monitoring")]
[Authorize]
public sealed class MonitoringController : ControllerBase
{
    private readonly RequestMetricsStore _metrics;
    private readonly SystemHealthService _healthService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        RequestMetricsStore metrics,
        SystemHealthService healthService,
        ILogger<MonitoringController> logger)
    {
        _metrics = metrics;
        _healthService = healthService;
        _logger = logger;
    }

    /// <summary>
    /// Full monitoring dashboard — process metrics + all services health + API stats.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        if (!HasMonitoringPermission())
            return Forbid();

        _logger.LogInformation("Monitoring dashboard accessed by {User}", GetCurrentUser());

        var process = _healthService.GetProcessMetrics();
        var services = await _healthService.GetServicesHealthAsync(ct);
        var apiMetrics = _metrics.GetSummary();

        return Ok(new
        {
            process,
            services,
            api = new
            {
                apiMetrics.TotalRequests,
                apiMetrics.TotalErrors,
                ErrorRate = apiMetrics.TotalRequests > 0
                    ? Math.Round((double)apiMetrics.TotalErrors / apiMetrics.TotalRequests * 100, 2)
                    : 0,
                apiMetrics.TopRoutes
            },
            generatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Recent API requests (last 50).
    /// </summary>
    [HttpGet("requests")]
    public IActionResult GetRecentRequests()
    {
        if (!HasMonitoringPermission()) return Forbid();
        return Ok(_metrics.GetSummary().RecentRequests);
    }

    /// <summary>
    /// Recent unhandled exceptions (last 100).
    /// </summary>
    [HttpGet("exceptions")]
    public IActionResult GetRecentExceptions()
    {
        if (!HasMonitoringPermission()) return Forbid();
        return Ok(_metrics.GetSummary().RecentExceptions);
    }

    /// <summary>
    /// Route-level performance statistics.
    /// </summary>
    [HttpGet("routes")]
    public IActionResult GetRouteStats()
    {
        if (!HasMonitoringPermission()) return Forbid();
        return Ok(_metrics.GetSummary().TopRoutes);
    }

    /// <summary>
    /// Health check for all downstream services.
    /// </summary>
    [HttpGet("services")]
    public async Task<IActionResult> GetServicesHealth(CancellationToken ct)
    {
        if (!HasMonitoringPermission()) return Forbid();
        var health = await _healthService.GetServicesHealthAsync(ct);
        return Ok(health);
    }

    /// <summary>
    /// Gateway process memory and CPU metrics.
    /// </summary>
    [HttpGet("process")]
    public IActionResult GetProcessMetrics()
    {
        if (!HasMonitoringPermission()) return Forbid();
        return Ok(_healthService.GetProcessMetrics());
    }

    private bool HasMonitoringPermission()
    {
        var permissions = User.FindAll("permission").Select(c => c.Value);
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        return roles.Any(r => r is "SuperAdmin" or "Admin")
            || permissions.Any(p => p == "monitoring:view");
    }

    private string GetCurrentUser()
        => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
}
