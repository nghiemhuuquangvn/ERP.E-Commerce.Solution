using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

/// <summary>
/// Creates the inspections of P4-3 inside the save that applies the receipt / the report that needs them (BR-QC-02), so an inspection exists exactly when its source counts and a
/// failed posting leaves none. A plan that does not resolve (none linked, Draft, Obsolete, no version in force) means no inspection (owner decision, P4-3).
/// </summary>
public interface IQualityInspectionCreator
{
    /// <summary>The IPQC plan that applies to the operation today, or null.</summary>
    Task<InspectionPlan?> ResolveIpqcAsync(ProductionOrder order, ProductionOrderOperation op, CancellationToken ct = default);

    /// <summary>The Pending IPQC of a report that is being applied (the order has already held its Good quantity).</summary>
    Task CreateIpqcAsync(ProductionOrder order, ProductionOrderOperation op, OperationReport report, InspectionPlan plan, CancellationToken ct = default);

    /// <summary>One FQC per Posted receipt line that is on quality hold, when the item requires one and its plan resolves. Safe to call twice (a line is inspected once).</summary>
    Task CreateFqcAsync(ProductionOrder order, MaterialTransaction receipt, CancellationToken ct = default);

    /// <summary>The report is reversed: a Pending inspection of it is cancelled; returns the Good quantity it held (0 when none). Refuses (<c>MFG_REV_003</c>) when the inspection has a result.</summary>
    Task<decimal> CancelPendingForReportAsync(Guid reportId, CancellationToken ct = default);

    /// <summary>A receipt is reversed (its reversal is Posted): the Pending inspections of its lines are cancelled.</summary>
    Task CancelPendingForReceiptAsync(MaterialTransaction receipt, CancellationToken ct = default);

    /// <summary>Checks before a reversal is stored: the latest inspection of a receipt line / report must not have a result. Returns what a reversal would cancel.</summary>
    Task CheckReceiptReversibleAsync(MaterialTransaction receipt, CancellationToken ct = default);

    Task<decimal> CheckReportReversibleAsync(Guid reportId, CancellationToken ct = default);
}

public sealed class QualityInspectionCreator(
    IPlantRepository plants, IPlantClock clock, IInspectionPlanResolver resolver, IItemProfileRepository profiles, IInspectionRepository inspections, IDocumentNumberService numbers)
    : IQualityInspectionCreator
{
    private async Task<(Plant Plant, DateOnly Today)> TodayAsync(Guid plantId, CancellationToken ct)
    {
        var plant = await plants.GetAsync(plantId, ct) ?? throw new EntityNotFoundException(nameof(Plant), plantId);
        return (plant, clock.Today(plant));
    }

    public async Task<InspectionPlan?> ResolveIpqcAsync(ProductionOrder order, ProductionOrderOperation op, CancellationToken ct = default)
    {
        if (op.IpqcInspectionPlanId is null) return null;
        var (_, today) = await TodayAsync(order.PlantId, ct);
        var plan = await resolver.ResolveAsync(op.IpqcInspectionPlanId, today, ct);
        return plan is { Type: InspectionType.Ipqc } ? plan : null;
    }

    public async Task CreateIpqcAsync(ProductionOrder order, ProductionOrderOperation op, OperationReport report, InspectionPlan plan, CancellationToken ct = default)
    {
        var (_, today) = await TodayAsync(order.PlantId, ct);
        var docNo = await numbers.NextAsync(DocumentTypes.Inspection, order.PlantId, today.ToDateTime(TimeOnly.MinValue), ct);
        inspections.Add(Inspection.CreateIpqc(docNo, plan, order.Id, order.OrderNo, op.Id, op.OperationSeq, report.Id, order.ItemId, order.ItemCode, order.ItemName, report.GoodQty));
    }

    public async Task CreateFqcAsync(ProductionOrder order, MaterialTransaction receipt, CancellationToken ct = default)
    {
        var lines = receipt.ReceiptLines.Where(l => l.Status == StockLineStatus.Posted && l.IsQcHold && l.PostedLotId is not null).ToList();
        if (lines.Count == 0) return;

        var (_, today) = await TodayAsync(order.PlantId, ct);
        var existing = (await inspections.ListForReceiptLinesAsync(lines.Select(l => l.Id).ToList(), ct)).Select(i => i.ReceiptLineId).ToHashSet();
        foreach (var line in lines.Where(l => !existing.Contains(l.Id)))
        {
            var profile = await profiles.GetAsync(line.ItemId, order.PlantId, ct);
            if (profile is not { RequiresFqc: true }) continue;
            var plan = await resolver.ResolveAsync(profile.FqcInspectionPlanId, today, ct);
            if (plan is not { Type: InspectionType.Fqc }) continue;           // no Approved plan in force: the lot stays held and is released by hand (owner decision)

            var docNo = await numbers.NextAsync(DocumentTypes.Inspection, order.PlantId, today.ToDateTime(TimeOnly.MinValue), ct);
            var name = line.ItemId == order.ItemId ? order.ItemName : line.ItemCode;
            inspections.Add(Inspection.CreateFqc(docNo, plan, order.Id, order.OrderNo, receipt.Id, line.Id, line.ItemId, line.ItemCode, name, line.PostedLotId!.Value, line.LotNumber, line.Qty));
        }
    }

    public async Task<decimal> CheckReportReversibleAsync(Guid reportId, CancellationToken ct = default)
    {
        var latest = (await inspections.ListForReportAsync(reportId, ct)).LastOrDefault();
        if (latest is null || latest.Status == InspectionStatus.Cancelled) return 0m;
        MfgRules.ThrowIf(latest.HasResult, MfgErrors.Rev003, ("inspection", latest.DocNo));
        return latest.LotQty;                                                     // Pending: it is cancelled with the report and its hold is lifted
    }

    public async Task<decimal> CancelPendingForReportAsync(Guid reportId, CancellationToken ct = default)
    {
        var latest = (await inspections.ListForReportAsync(reportId, ct)).LastOrDefault();
        if (latest is not { Status: InspectionStatus.Pending }) return 0m;
        latest.Cancel();
        return latest.LotQty;
    }

    public async Task CheckReceiptReversibleAsync(MaterialTransaction receipt, CancellationToken ct = default)
    {
        foreach (var latest in await LatestPerLineAsync(receipt, ct))
            MfgRules.ThrowIf(latest.HasResult, MfgErrors.Fg004, ("reason", $"phiếu kiểm tra {latest.DocNo} của lô {latest.LotNumber ?? latest.ItemCode} đã có kết quả"));
    }

    public async Task CancelPendingForReceiptAsync(MaterialTransaction receipt, CancellationToken ct = default)
    {
        foreach (var latest in await LatestPerLineAsync(receipt, ct))
            if (latest.Status == InspectionStatus.Pending) latest.Cancel();
    }

    /// <summary>The newest inspection of each receipt line (a re-inspection supersedes the Failed one it repeats); cancelled ones are not inspections any more.</summary>
    private async Task<List<Inspection>> LatestPerLineAsync(MaterialTransaction receipt, CancellationToken ct)
    {
        var ids = receipt.ReceiptLines.Select(l => l.Id).ToList();
        if (ids.Count == 0) return [];
        return (await inspections.ListForReceiptLinesAsync(ids, ct)).GroupBy(i => i.ReceiptLineId).Select(g => g.Last()).Where(i => i.Status != InspectionStatus.Cancelled).ToList();
    }
}

/// <summary>
/// The Warehouse half of releasing a held finished-goods lot (BR-QC-05): move it to the finished-goods warehouse of the order (only when it is not there yet) and make it Available.
/// Two idempotent calls per line (keys <c>&lt;line key&gt;Q1 / Q2</c>), so a release that stopped halfway - by the interim button or by an FQC Pass - is finished by calling again.
/// The caller records <see cref="MaterialTransaction.RecordQcReleased"/> and saves.
/// </summary>
public interface IQuarantineRelease
{
    Task ReleaseAsync(ProductionOrder order, MaterialTransactionLine line, string reason, CancellationToken ct = default);
}

public sealed class QuarantineRelease(IWarehouseStockClient stock) : IQuarantineRelease
{
    public async Task ReleaseAsync(ProductionOrder order, MaterialTransactionLine line, string reason, CancellationToken ct = default)
    {
        var lot = line.PostedLotId ?? throw MfgRules.Invalid($"Line {line.LineNo} has no lot to release.");
        if (line.WarehouseId != order.OutputWarehouseId)
            await stock.RecordMovementAsync(new("SubcontractTransfer", line.ItemId, line.Qty, line.MovementKey + "Q1", WarehouseId: line.WarehouseId, ToWarehouseId: order.OutputWarehouseId,
                LotId: lot, Reason: reason, SourceOrderId: order.Id), ct);
        await stock.RecordMovementAsync(new("QcRelease", line.ItemId, line.Qty, line.MovementKey + "Q2", WarehouseId: order.OutputWarehouseId, LotId: lot, Reason: reason, SourceOrderId: order.Id), ct);
    }
}
