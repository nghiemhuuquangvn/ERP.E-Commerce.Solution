using ERP.Shared.Infrastructure.Data;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.BackgroundJobs;

/// <summary>
/// The safety net under the carriers' webhooks: a parcel still in a carrier's hands that has not been heard of for a while is asked about. A webhook that
/// never came (the carrier cannot reach the shop, the shop's address changed) would otherwise leave the order "shipped" for ever and a cash-on-delivery
/// payment uncollected. Same application path as the webhook (<see cref="CarrierTrackingService.ApplyAsync"/>). No user: tenants are found across
/// tenants (ids only) and each is processed inside its own TenantScope and DI scope.
/// </summary>
public class CarrierTrackingSyncJob : IHostedService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(6);
    private const int BatchPerTenant = 50;

    private readonly IServiceProvider _services;
    private readonly ILogger<CarrierTrackingSyncJob> _logger;
    private CancellationTokenSource? _cts;
    private Task? _task;

    public CarrierTrackingSyncJob(IServiceProvider services, ILogger<CarrierTrackingSyncJob> logger)
    {
        _services = services;
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
        await Task.Delay(TimeSpan.FromMinutes(2), ct).ContinueWith(_ => { }, CancellationToken.None);
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(StaleAfter, ct); }
            catch (Exception ex) { _logger.LogError(ex, "CarrierTrackingSyncJob error"); }
            await Task.Delay(Interval, ct).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    /// <summary>One pass over every tenant. Returns how many shipments were asked about.</summary>
    public async Task<int> RunOnceAsync(TimeSpan staleAfter, CancellationToken ct)
    {
        List<Guid> tenants;
        using (var scope = _services.CreateScope())
            tenants = await scope.ServiceProvider.GetRequiredService<IShippingUnitOfWork>().Shipments.GetTenantsWithOpenShipmentsAsync(ct);

        var asked = 0;
        foreach (var tenantId in tenants.Where(t => t != Guid.Empty))
        {
            try
            {
                using var tenantScope = TenantScope.Begin(tenantId);
                using var scope = _services.CreateScope();
                asked += await SyncTenantAsync(scope.ServiceProvider, staleAfter, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Carrier sync failed for tenant {Tenant}", tenantId);   // one tenant never stops the others
            }
        }
        return asked;
    }

    private static async Task<int> SyncTenantAsync(IServiceProvider sp, TimeSpan staleAfter, CancellationToken ct)
    {
        var uow = sp.GetRequiredService<IShippingUnitOfWork>();
        var carriers = sp.GetRequiredService<ICarrierProviderResolver>();
        var tracking = sp.GetRequiredService<CarrierTrackingService>();

        var asked = 0;
        foreach (var shipment in await uow.Shipments.GetStaleInCarrierHandsAsync(DateTime.UtcNow - staleAfter, BatchPerTenant, ct))
        {
            var code = await tracking.CarrierCodeOfAsync(shipment, ct);
            if (code == null) { shipment.MarkCarrierSynced(); continue; }

            try
            {
                var resolved = await carriers.ResolveAsync(code, ct);
                var status = await resolved.Provider.GetStatusAsync(resolved.Credentials, shipment.TrackingNumber!, ct);
                asked++;
                if (status.Success)
                    await tracking.ApplyAsync(shipment, status.Status, status.CarrierStatus ?? string.Empty, status.Description, null, ct);
                else
                    shipment.MarkCarrierSynced();   // this carrier cannot be polled (or is down): do not ask again every tick
            }
            catch (ERP.Shared.Infrastructure.Exceptions.BusinessRuleException)
            {
                shipment.MarkCarrierSynced();       // the shop's account is off: nothing to ask
            }
        }

        await uow.SaveChangesAsync(ct);
        return asked;
    }
}
