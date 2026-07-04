using ERP.PRM.Domain.Enums;
using ERP.PRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.PRM.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs every minute to auto-activate promotions whose StartDate has arrived,
/// expire promotions/flash-sales past their EndDate, and complete flash-sales past EndTime.
/// </summary>
public class PromotionSchedulerJob : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PromotionSchedulerJob> _logger;
    private CancellationTokenSource? _cts;
    private Task? _task;

    public PromotionSchedulerJob(IServiceProvider serviceProvider, ILogger<PromotionSchedulerJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _task = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_task != null)
            await Task.WhenAny(_task, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PromotionSchedulerJob error");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var now = DateTime.UtcNow;

        // Auto-activate Draft promotions whose start date has arrived
        var toActivate = await db.Promotions
            .Where(p => p.Status == PromotionStatus.Draft && p.StartDate <= now && p.EndDate > now)
            .ToListAsync(ct);

        foreach (var p in toActivate)
        {
            p.Activate();
            _logger.LogInformation("Auto-activated promotion {Id} '{Name}'", p.Id, p.Name);
        }

        // Auto-expire Active/Paused promotions past EndDate
        var toExpire = await db.Promotions
            .Where(p => (p.Status == PromotionStatus.Active || p.Status == PromotionStatus.Paused) && p.EndDate < now)
            .ToListAsync(ct);

        foreach (var p in toExpire)
        {
            p.Expire();
            _logger.LogInformation("Auto-expired promotion {Id} '{Name}'", p.Id, p.Name);
        }

        // Auto-start Scheduled flash sales whose StartTime has arrived
        var flashToStart = await db.FlashSales
            .Include(fs => fs.Items)
            .Where(fs => fs.Status == FlashSaleStatus.Scheduled && fs.StartTime <= now && fs.EndTime > now)
            .ToListAsync(ct);

        foreach (var fs in flashToStart)
        {
            fs.Start();
            _logger.LogInformation("Auto-started flash sale {Id} '{Name}'", fs.Id, fs.Name);
        }

        // Auto-complete Active flash sales past EndTime
        var flashToComplete = await db.FlashSales
            .Where(fs => fs.Status == FlashSaleStatus.Active && fs.EndTime < now)
            .ToListAsync(ct);

        foreach (var fs in flashToComplete)
        {
            fs.Complete();
            _logger.LogInformation("Auto-completed flash sale {Id} '{Name}'", fs.Id, fs.Name);
        }

        if (toActivate.Any() || toExpire.Any() || flashToStart.Any() || flashToComplete.Any())
            await db.SaveChangesAsync(ct);
    }
}
