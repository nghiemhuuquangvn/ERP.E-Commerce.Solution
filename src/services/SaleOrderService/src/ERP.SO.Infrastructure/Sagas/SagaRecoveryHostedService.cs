using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Sagas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.Sagas;

/// <summary>
/// Periodically surfaces sagas that haven't progressed within a reasonable time —
/// GetStuckSagasAsync existed on the repository already but nothing ever called it.
/// Deliberately excludes PaymentPending from alerting: waiting on an external payment
/// gateway can legitimately take a long time (or the customer may abandon checkout),
/// so that step is not "stuck" in the same sense a should-be-fast inventory/shipment
/// step is. No auto-retry — logging is the deliverable here.
/// </summary>
public class SagaRecoveryHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StuckTimeout = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SagaRecoveryHostedService> _logger;

    public SagaRecoveryHostedService(IServiceProvider serviceProvider, ILogger<SagaRecoveryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOrderFulfillmentSagaRepository>();

                var stuck = await repository.GetStuckSagasAsync(StuckTimeout, stoppingToken);
                foreach (var saga in stuck)
                {
                    if (saga.CurrentStep == SagaStep.PaymentPending)
                        continue; // Legitimately can take a long time — not an alert-worthy stall.

                    _logger.LogWarning(
                        "Saga {SagaId} for Order {OrderId} has been stuck at {Step} since {LastUpdatedAt}",
                        saga.Id, saga.OrderId, saga.CurrentStep, saga.LastUpdatedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for stuck sagas");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
