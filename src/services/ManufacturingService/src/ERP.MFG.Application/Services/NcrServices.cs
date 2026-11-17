using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

/// <summary>
/// The bookkeeping that ties non-conformance reports to the rest of the order (P4-4). Every method works on tracked entities and never saves: the caller's save carries the change together
/// with its own, so an NCR exists exactly when its inspection failed and a rework line counts as executed exactly when its order is Completed.
/// </summary>
public interface INcrProgress
{
    /// <summary>T4.10.1: the inspection just Failed - its NCR is created (once). Called in the same save as the inspection result.</summary>
    Task CreateForFailedInspectionAsync(Inspection failed, string by, CancellationToken ct = default);

    /// <summary>BR-QC-06: a re-inspection of the Failed inspection replaces its NCR while no line started (the NCR is Cancelled); a started or closed NCR refuses it (<c>MFG_NCR_004</c>).</summary>
    Task CancelForReinspectionAsync(Inspection failed, string by, CancellationToken ct = default);

    /// <summary>The rework order is Completed: its disposition line is executed and the NCR closes with the last line.</summary>
    Task OnReworkOrderCompletedAsync(ProductionOrder order, string by, DateTime now, CancellationToken ct = default);

    /// <summary>The rework order was cancelled before any work: its line can be executed again.</summary>
    Task OnReworkOrderCancelledAsync(ProductionOrder order, string by, CancellationToken ct = default);

    /// <summary>The NCR closed: the lot of its receipt line is settled (<c>QcReleasedAt</c> is set), so it no longer counts as held in the close checklist.</summary>
    Task SettleAsync(NonConformance ncr, DateTime now, CancellationToken ct = default);
}

public sealed class NcrProgress(INcrRepository ncrs, IPlantRepository plants, IPlantClock clock, IDocumentNumberService numbers, IStockDocumentRepository documents) : INcrProgress
{
    public async Task CreateForFailedInspectionAsync(Inspection failed, string by, CancellationToken ct = default)
    {
        if (await ncrs.GetByInspectionAsync(failed.Id, ct) is not null) return;
        var plant = await plants.GetAsync(failed.PlantId, ct) ?? throw new EntityNotFoundException(nameof(Plant), failed.PlantId);
        var number = await numbers.NextAsync(DocumentTypes.Ncr, Guid.Empty, clock.Today(plant).ToDateTime(TimeOnly.MinValue), ct);
        ncrs.Add(NonConformance.CreateFromInspection(number, failed, by));
    }

    public async Task CancelForReinspectionAsync(Inspection failed, string by, CancellationToken ct = default)
    {
        var ncr = await ncrs.GetByInspectionAsync(failed.Id, ct);
        if (ncr is null || ncr.Status == NcrStatus.Cancelled) return;
        ncr.Cancel(by);
    }

    public async Task OnReworkOrderCompletedAsync(ProductionOrder order, string by, DateTime now, CancellationToken ct = default)
    {
        if (!order.IsRework) return;
        var ncr = await ncrs.GetByReworkOrderAsync(order.Id, ct);
        var line = ncr?.AllDispositions.FirstOrDefault(d => !d.IsDeleted && !d.IsExecuted && d.ReworkOrderId == order.Id);
        if (ncr is null || line is null) return;
        if (ncr.MarkExecuted(line.LineNo, null, null, null, by, now)) await SettleAsync(ncr, now, ct);
    }

    public async Task OnReworkOrderCancelledAsync(ProductionOrder order, string by, CancellationToken ct = default)
    {
        if (!order.IsRework) return;
        (await ncrs.GetByReworkOrderAsync(order.Id, ct))?.ClearReworkOrder(order.Id, by);
    }

    public async Task SettleAsync(NonConformance ncr, DateTime now, CancellationToken ct = default)
    {
        if (ncr.ReceiptDocumentId is not { } documentId || ncr.ReceiptLineId is not { } lineId) return;
        var document = await documents.GetAsync(documentId, ct);
        var line = document?.ReceiptLines.FirstOrDefault(l => l.Id == lineId);
        if (document is null || line is null || line.QcReleasedAt is not null) return;
        document.RecordQcReleased(line, now);
    }
}
