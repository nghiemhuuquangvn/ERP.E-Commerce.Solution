using ERP.Shared.Infrastructure.Data;
using ERP.SO.Application.Sagas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.SO.Infrastructure.Sagas;

/// <summary>
/// Looks for sagas that stopped moving (ROADMAP P1-5). A Running saga that has waited in a step longer than that step's timeout
/// (<see cref="SagaOptions"/>, configuration section <c>Saga</c>) gets the command of the step published again; once the retries are used up the saga is
/// compensated: the payment is refunded, the order is cancelled and the reservation released. A saga waiting for the order's approval is never touched.
/// Every saga is handled in its own tenant scope and its own DI scope, so one failing order does not stop the others.
/// </summary>
public class SagaRecoveryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SagaOptions _options;
    private readonly ILogger<SagaRecoveryHostedService> _logger;

    public SagaRecoveryHostedService(IServiceProvider serviceProvider, IOptions<SagaOptions> options, ILogger<SagaRecoveryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for stuck sagas");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.CheckIntervalSeconds, 5)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One pass: returns how many sagas were retried and how many were compensated.</summary>
    public async Task<(int Retried, int TimedOut)> RunOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        List<(Guid SagaId, Guid TenantId)> candidates;
        using (var scope = _serviceProvider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOrderFulfillmentSagaRepository>();
            candidates = (await repository.GetStuckSagasAsync(_options.ShortestTimeout, ct)).Select(s => (s.Id, s.TenantId)).ToList();
        }

        int retried = 0, timedOut = 0;
        foreach (var (sagaId, tenantId) in candidates)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                using var tenant = TenantScope.Begin(tenantId);
                var saga = scope.ServiceProvider.GetRequiredService<OrderFulfillmentSaga>();
                switch (await saga.HandleStepTimeoutAsync(sagaId, _options, nowUtc, force: false, ct))
                {
                    case SagaTimeoutOutcome.Retried: retried++; break;
                    case SagaTimeoutOutcome.TimedOut: timedOut++; break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not recover saga {SagaId} of tenant {TenantId}", sagaId, tenantId);
            }
        }
        return (retried, timedOut);
    }
}
