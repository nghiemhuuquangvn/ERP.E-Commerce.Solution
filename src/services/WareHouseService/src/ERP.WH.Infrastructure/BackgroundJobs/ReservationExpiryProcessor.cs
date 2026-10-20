using ERP.Shared.Infrastructure.Data;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.Shared.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.WH.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Sweeps Reservations past their HoldUntil and auto-releases them — previously
    /// HoldUntil/IsExpired()/Expire() existed on Reservation but nothing ever called
    /// them, so stock reserved for an order that never shipped or was explicitly
    /// cancelled stayed locked forever. Reuses ReleaseReservationCommandHandler's own
    /// soft-hold release logic (Inventory/InventoryLocationStock/InventoryLot), only
    /// differing in the terminal status it sets (Expired vs Released).
    /// </summary>
    public class ReservationExpiryProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReservationExpiryProcessor> _logger;
        private readonly int _pollingIntervalMs = 5 * 60 * 1000;

        public ReservationExpiryProcessor(IServiceProvider serviceProvider, ILogger<ReservationExpiryProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReservationExpiryProcessor started. Polling interval: {PollingIntervalMs}ms", _pollingIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExpireReservationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error expiring reservations");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }

            _logger.LogInformation("ReservationExpiryProcessor stopped");
        }

        private async Task ExpireReservationsAsync(CancellationToken cancellationToken)
        {
            // No user here: find the tenants with work across tenants, then release each
            // tenant's reservations inside its own TenantScope (and its own DI scope).
            List<Guid> tenants;
            using (var lookupScope = _serviceProvider.CreateScope())
                tenants = await lookupScope.ServiceProvider.GetRequiredService<IWarehouseUnitOfWork>()
                    .Reservations.GetTenantsWithExpiredAsync(DateTime.UtcNow, cancellationToken);

            foreach (var tenantId in tenants.Where(t => t != Guid.Empty))
            {
                using (TenantScope.Begin(tenantId))
                    await ExpireTenantReservationsAsync(cancellationToken);
            }
        }

        private async Task ExpireTenantReservationsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IWarehouseUnitOfWork>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var expired = await uow.Reservations.GetExpiredAsync(DateTime.UtcNow, cancellationToken);
            if (expired.Count == 0)
                return;

            _logger.LogInformation("Found {Count} expired reservations to release", expired.Count);

            foreach (var reservation in expired)
            {
                await ReleaseReservationCommandHandler.ReleaseAllocationsAsync(uow, reservation, cancellationToken);
                reservation.Expire();
                await uow.Reservations.UpdateAsync(reservation, cancellationToken);
                await uow.SaveChangesAsync(cancellationToken);

                foreach (var productId in reservation.ReservedItems.Select(i => i.ProductId).Distinct())
                    await cache.RemoveAsync($"wh:inventory:{productId}", cancellationToken);
            }
        }
    }
}
