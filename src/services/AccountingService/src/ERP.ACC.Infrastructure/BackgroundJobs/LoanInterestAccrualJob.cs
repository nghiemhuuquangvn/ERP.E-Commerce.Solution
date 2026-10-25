using ERP.ACC.Application.Handlers;
using ERP.ACC.Infrastructure.Persistence;
using ERP.Shared.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Infrastructure.BackgroundJobs;

/// <summary>
/// Month-end accrual of loan interest (ROADMAP P1-2l). Checks every hour; once a day it accrues, for each tenant whose closing policy switched it on, the interest of
/// the posted loans up to the last day of the previous month. The work is the <see cref="AccrueLoanInterestCommand"/> (only the difference since the last accrual
/// is posted), so a second tick, a restart or a manual run the same day posts nothing more. A closed period is skipped, not back-filled into today's.
/// </summary>
public class LoanInterestAccrualJob : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoanInterestAccrualJob> _logger;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private DateTime _lastRunDay = DateTime.MinValue;

    public LoanInterestAccrualJob(IServiceProvider serviceProvider, ILogger<LoanInterestAccrualJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>The last day of the month before the one <paramref name="today"/> is in.</summary>
    public static DateTime AccrualDate(DateTime today) => new DateTime(today.Year, today.Month, 1).AddDays(-1);

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
        // let the service finish starting (migrations, consumers) before the first pass
        await Task.Delay(TimeSpan.FromMinutes(1), ct).ContinueWith(_ => { }, CancellationToken.None);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                if (today > _lastRunDay)
                {
                    await TickAsync(today, ct);
                    _lastRunDay = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoanInterestAccrualJob error");
            }

            await Task.Delay(TimeSpan.FromHours(1), ct).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    /// <summary>One pass: the tenants that opted in, each accrued inside its own tenant scope and DI scope. Returns the number of tenants handled.</summary>
    public async Task<int> TickAsync(DateTime today, CancellationToken ct)
    {
        var asOf = AccrualDate(today);
        List<Guid> tenants;
        using (var lookupScope = _serviceProvider.CreateScope())
        {
            var lookup = lookupScope.ServiceProvider.GetRequiredService<AccountingDbContext>();
            tenants = await lookup.ClosingPolicies.IgnoreQueryFilters()
                .Where(p => p.AutoAccrueLoanInterest && !p.IsDeleted && p.TenantId != Guid.Empty)
                .Select(p => p.TenantId).Distinct().ToListAsync(ct);
        }

        foreach (var tenantId in tenants)
        {
            try
            {
                using (TenantScope.Begin(tenantId))
                using (var scope = _serviceProvider.CreateScope())
                {
                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new AccrueLoanInterestCommand { AsOfDate = asOf, SkipClosedPeriod = true }, ct);
                    if (result.TotalAccrued > 0m)
                        _logger.LogInformation("Accrued loan interest {Total} to {AsOf:yyyy-MM-dd} for tenant {Tenant}", result.TotalAccrued, asOf, tenantId);
                }
            }
            catch (Exception ex)
            {
                // one tenant's failure must not stop the others
                _logger.LogError(ex, "Loan interest accrual failed for tenant {Tenant}", tenantId);
            }
        }
        return tenants.Count;
    }
}
