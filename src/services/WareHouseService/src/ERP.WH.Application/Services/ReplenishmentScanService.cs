using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Services;

public class ReplenishmentScanResult
{
    public int Checked { get; set; }
    public int BelowMinimum { get; set; }
    public int AlertsSent { get; set; }
}

public interface IReplenishmentScanService
{
    /// <summary>Re-sends the low-stock alert of every product of the current tenant that is below its minimum and has not been alerted for a day.</summary>
    Task<ReplenishmentScanResult> ScanAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The daily scan (and the "scan now" button): a movement alerts once per time below the minimum, so a request that was
/// dismissed, or an alert lost on the way, would never come back without this. Purchasing decides whether a request is still needed
/// (it knows the goods already on order), so the scan only repeats the alert.
/// </summary>
public class ReplenishmentScanService : IReplenishmentScanService
{
    public static readonly TimeSpan RepeatAfter = TimeSpan.FromHours(24);

    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;

    public ReplenishmentScanService(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
    }

    public async Task<ReplenishmentScanResult> ScanAsync(CancellationToken cancellationToken)
    {
        var inventories = await _uow.Inventories.GetAllAsync(cancellationToken);
        var result = new ReplenishmentScanResult { Checked = inventories.Count };
        var now = DateTime.UtcNow;

        foreach (var inventory in inventories)
        {
            var wasAlerted = inventory.LowStockAlertedAt;
            if (inventory.IsLowStock()) result.BelowMinimum++;

            var alert = inventory.EvaluateLowStockAlert(now, RepeatAfter);
            if (alert != null)
            {
                _outboxWriter.Stage(alert);
                result.AlertsSent++;
            }

            if (alert != null || inventory.LowStockAlertedAt != wasAlerted)
                await _uow.Inventories.UpdateAsync(inventory, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return result;
    }
}
