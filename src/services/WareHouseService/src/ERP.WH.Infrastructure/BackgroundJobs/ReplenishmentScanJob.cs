using ERP.Shared.Infrastructure.Data;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.WH.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Once a day (from <c>Replenishment:ScanHour</c>, UTC, default 2) runs the replenishment scan for every tenant, each inside
    /// its own TenantScope and DI scope. It only repeats the low-stock alert; Purchasing decides whether a request is needed.
    /// </summary>
    public class ReplenishmentScanJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReplenishmentScanJob> _logger;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(15);
        private DateTime _lastRunDate = DateTime.MinValue;

        public ReplenishmentScanJob(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<ReplenishmentScanJob> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReplenishmentScanJob started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    if (IsDue(now, _configuration.GetValue("Replenishment:ScanHour", 2), _lastRunDate))
                    {
                        await ScanAllTenantsAsync(stoppingToken);
                        _lastRunDate = now.Date;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning stock for replenishment");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }

        /// <summary>The scan runs once per UTC day, at the first poll at or after the configured hour.</summary>
        public static bool IsDue(DateTime nowUtc, int scanHour, DateTime lastRunDate) =>
            nowUtc.Hour >= scanHour && lastRunDate < nowUtc.Date;

        private async Task ScanAllTenantsAsync(CancellationToken cancellationToken)
        {
            List<Guid> tenants;
            using (var lookupScope = _serviceProvider.CreateScope())
                tenants = await lookupScope.ServiceProvider.GetRequiredService<IWarehouseUnitOfWork>()
                    .Inventories.GetTenantsWithInventoryAsync(cancellationToken);

            foreach (var tenantId in tenants.Where(t => t != Guid.Empty))
            {
                try
                {
                    using (TenantScope.Begin(tenantId))
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var result = await scope.ServiceProvider.GetRequiredService<IReplenishmentScanService>().ScanAsync(cancellationToken);
                        if (result.AlertsSent > 0)
                            _logger.LogInformation("Replenishment scan: tenant {TenantId}, {Alerts} alert(s) for {Below} product(s) below minimum",
                                tenantId, result.AlertsSent, result.BelowMinimum);
                    }
                }
                catch (Exception ex)
                {
                    // One tenant failing must not stop the others.
                    _logger.LogError(ex, "Replenishment scan failed for tenant {TenantId}", tenantId);
                }
            }
        }
    }
}
