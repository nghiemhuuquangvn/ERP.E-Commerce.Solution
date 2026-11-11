using ERP.MFG.Application.Abstractions;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Seeding;

/// <summary>
/// A throw-away check of Warehouse W7 against the real internal stock API (dev / test only, behind <c>Manufacturing:EnableDemoSeed</c>, like
/// <see cref="RunStockCycleCommand"/>): a lot on quality hold is split by a partial move, the sub-lot is released alone, the held rest is issued for
/// rework (only with the flag), and - when a component is named - a reservation issue over several lots answers the lots it used. It writes nothing in
/// Manufacturing's own tables. The internal API is not reachable from outside, which is why the check runs inside this service.
/// </summary>
public sealed record LotCycleRequest(
    string OrderNumber,
    Guid FinishedProductId,
    Guid QuarantineWarehouseId,
    Guid TargetWarehouseId,
    Guid? ComponentProductId = null,
    Guid? RawWarehouseId = null,
    decimal FinishedQuantity = 10m,
    decimal SplitQuantity = 4m,
    decimal ReworkQuantity = 2m,
    decimal ComponentQuantity = 52.5m);

public sealed record LotCycleResult(bool Ok, string OrderNumber, List<StockCycleStep> Steps);

public sealed record RunLotCycleCommand(LotCycleRequest Request) : IRequest<LotCycleResult>;

public sealed class RunLotCycleHandler(IWarehouseStockClient stock, IProductLookupClient products) : IRequestHandler<RunLotCycleCommand, LotCycleResult>
{
    public async Task<LotCycleResult> Handle(RunLotCycleCommand command, CancellationToken ct)
    {
        var r = command.Request;
        var order = r.OrderNumber.Trim();
        var steps = new List<StockCycleStep>();

        bool Step(string name, bool ok, string detail) { steps.Add(new StockCycleStep(name, ok, detail)); return ok; }
        LotCycleResult Done() => new(steps.All(s => s.Ok), order, steps);
        string Key(string line) => $"{order}#{line}";
        static string Lots(StockMovementResult m) => string.Join("; ", (m.Lots ?? []).Select(l => $"{l.LotNumber} x{l.Quantity:0.######} @{l.UnitCost:0.######}{(l.ParentLotId is null ? "" : " (sub-lot)")}"));

        async Task<StockMovementResult?> Refused(string name, StockMovementRequest probe, string code)
        {
            try
            {
                var accepted = await stock.RecordMovementAsync(probe, ct);
                Step(name, false, $"accepted, expected {code}");
                return accepted;
            }
            catch (BusinessRuleException ex) when (ex.RuleName == code)
            {
                Step(name, true, $"refused with {code}");
                return null;
            }
        }

        try
        {
            var ids = new List<Guid> { r.FinishedProductId };
            if (r.ComponentProductId is { } componentId) ids.Add(componentId);
            var info = await products.LookupAsync(ids, ct);
            var finished = info.FirstOrDefault(p => p.Id == r.FinishedProductId);
            if (!Step("product-flags", finished != null, finished == null ? "The finished good is unknown to ProductService." : $"lot={finished.IsLotControlled} serial={finished.IsSerialControlled}")) return Done();
            var fLot = finished!.IsLotControlled;
            var fSerial = finished.IsSerialControlled;

            // 1. a lot arrives on quality hold in the quarantine warehouse
            var lotNumber = $"LC-{order}";
            var receipt = await stock.RecordMovementAsync(new("ProductionReceipt", r.FinishedProductId, r.FinishedQuantity, Key("1"), WarehouseId: r.QuarantineWarehouseId,
                LotNumber: lotNumber, ManufactureDate: DateTime.UtcNow.Date, UnitCost: 5m, InitialLotStatus: "QcHold", RequiresLot: fLot, RequiresSerial: fSerial), ct);
            if (!Step("receipt-on-hold", receipt.LotId != null && receipt.Lots is { Count: 1 }, $"lot {receipt.LotId} {lotNumber}; rows: {Lots(receipt)}")) return Done();
            var lot = receipt.LotId!.Value;

            // 2. a PART of it moves to the finished-goods warehouse: the part is a new sub-lot, the movement keeps the lot named in the request
            StockMovementRequest Split() => new("SubcontractTransfer", r.FinishedProductId, r.SplitQuantity, Key("2"), WarehouseId: r.QuarantineWarehouseId,
                ToWarehouseId: r.TargetWarehouseId, LotId: lot, RequiresLot: fLot, RequiresSerial: fSerial);
            var split = await stock.RecordMovementAsync(Split(), ct);
            var sub = split.Lots?.SingleOrDefault();
            if (!Step("split-transfer", split.LotId == lot && sub != null && sub.ParentLotId == lot && sub.Quantity == r.SplitQuantity && sub.WarehouseId == r.TargetWarehouseId,
                    $"movement lot {split.LotId}; rows: {Lots(split)}; sub-lot {sub?.LotId}")) return Done();
            var again = await stock.RecordMovementAsync(Split(), ct);
            if (!Step("split-replay", again.Replayed && again.Lots?.SingleOrDefault()?.LotId == sub!.LotId, $"replayed={again.Replayed}; sub-lot {again.Lots?.SingleOrDefault()?.LotId}")) return Done();
            await Refused("split-too-much", new("SubcontractTransfer", r.FinishedProductId, r.FinishedQuantity + 1, Key("2X"), WarehouseId: r.QuarantineWarehouseId,
                ToWarehouseId: r.TargetWarehouseId, LotId: lot, RequiresLot: fLot), "WH_LOT_QUANTITY");

            // 3. only the sub-lot is released; the held rest stays on hold
            var release = await stock.RecordMovementAsync(new("QcRelease", r.FinishedProductId, 0m, Key("3"), LotId: sub!.LotId, RequiresLot: fLot), ct);
            Step("release-sub-lot", release.Lots?.SingleOrDefault()?.LotId == sub.LotId, $"rows: {Lots(release)}");
            var lotsNow = await stock.ListLotsAsync(r.FinishedProductId, ct);
            var parentNow = lotsNow.FirstOrDefault(l => l.LotId == lot);
            var subNow = lotsNow.FirstOrDefault(l => l.LotId == sub.LotId);
            if (!Step("hold-and-release-states", parentNow?.Status == "QcHold" && parentNow.RemainingQuantity == r.FinishedQuantity - r.SplitQuantity
                                                  && subNow?.Status == "Available" && subNow.ParentLotId == lot,
                    $"parent {parentNow?.Status} {parentNow?.RemainingQuantity}; sub {subNow?.Status} {subNow?.RemainingQuantity} parent={subNow?.ParentLotId}")) return Done();

            // 4. rework consumes the held lot: refused without the flag, accepted with the flag, the lot and a reason
            await Refused("issue-held-without-flag", new("ProductionIssue", r.FinishedProductId, r.ReworkQuantity, Key("4"), LotId: lot, RequiresLot: fLot), "WH_LOT_NOT_USABLE");
            await Refused("issue-held-without-reason", new("ProductionIssue", r.FinishedProductId, r.ReworkQuantity, Key("4"), LotId: lot, RequiresLot: fLot, AllowHeldLot: true), "WH_REASON_REQUIRED");
            var rework = await stock.RecordMovementAsync(new("ProductionIssue", r.FinishedProductId, r.ReworkQuantity, Key("4"), LotId: lot, RequiresLot: fLot, RequiresSerial: fSerial,
                AllowHeldLot: true, Reason: $"Rework {order}"), ct);
            Step("issue-held-for-rework", !rework.Replayed && rework.Lots?.SingleOrDefault()?.LotId == lot, $"value {rework.ValueDelta}; rows: {Lots(rework)}");

            // 5. a reservation issue over several lots answers every lot it used
            if (r.ComponentProductId is { } component && r.RawWarehouseId is { } raw)
            {
                var cInfo = info.FirstOrDefault(p => p.Id == component);
                var cLot = cInfo?.IsLotControlled ?? false;
                var orderId = Guid.NewGuid();
                var reservation = await stock.ReserveAsync(orderId, order, raw, [new StockReservationLine(component, r.ComponentQuantity, SourceLineId: orderId)], ct);
                var reservedLots = reservation.Lines.SelectMany(l => l.Lots).ToList();
                if (!Step("reserve-components", reservation.Status == "Reserved", $"{reservedLots.Count} lots reserved: {string.Join(", ", reservedLots.Select(l => $"{l.LotNumber} x{l.Quantity}"))}")) return Done();
                var issue = await stock.RecordMovementAsync(new("ProductionIssue", component, r.ComponentQuantity, Key("5"), WarehouseId: raw, ReservationId: reservation.ReservationId,
                    SourceOrderId: orderId, RequiresLot: cLot), ct);
                var sum = (issue.Lots ?? []).Sum(l => l.Quantity);
                Step("issue-breakdown", (issue.Lots?.Count ?? 0) == reservedLots.Count && sum == r.ComponentQuantity && (reservedLots.Count < 2 || issue.LotId == null),
                    $"movement lot {issue.LotId}; {issue.Lots?.Count} rows summing {sum}: {Lots(issue)}");
                var replay = await stock.RecordMovementAsync(new("ProductionIssue", component, r.ComponentQuantity, Key("5"), WarehouseId: raw, ReservationId: reservation.ReservationId,
                    SourceOrderId: orderId, RequiresLot: cLot), ct);
                Step("issue-breakdown-replay", replay.Replayed && Lots(replay) == Lots(issue), $"replayed={replay.Replayed}; first: {Lots(issue)} | replay: {Lots(replay)}");
            }
        }
        catch (Exception ex) when (ex is BusinessRuleException or ConcurrencyException or EntityNotFoundException)
        {
            var code = ex is BusinessRuleException b ? b.RuleName : ex is ConcurrencyException c ? c.ErrorCode : null;
            Step("stopped", false, $"{code}: {ex.Message}");
        }

        return Done();
    }
}
