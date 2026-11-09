using ERP.MFG.Application.Abstractions;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Seeding;

/// <summary>
/// A throw-away production-order cycle against the real Warehouse API (dev / test only, behind <c>Manufacturing:EnableDemoSeed</c>): reserve,
/// issue in two pieces, return, scrap, receive the finished good under quality hold, release, block, reverse. It writes nothing in Manufacturing's
/// own tables; its purpose is to prove the service-to-service contract (token, idempotency keys, lot / serial flags) on a live stack.
/// </summary>
public sealed record StockCycleRequest(
    string OrderNumber,
    Guid ComponentProductId,
    Guid FinishedProductId,
    Guid RawWarehouseId,
    Guid QuarantineWarehouseId,
    Guid? ProductionOrderId = null,
    decimal ComponentQuantity = 8m,
    decimal FirstIssueQuantity = 3m,
    decimal ReturnQuantity = 1m,
    decimal ScrapQuantity = 0.5m,
    decimal FinishedQuantity = 5m);

public sealed record StockCycleStep(string Name, bool Ok, string Detail);

public sealed record StockCycleResult(bool Ok, Guid ProductionOrderId, string OrderNumber, List<StockCycleStep> Steps);

public sealed record RunStockCycleCommand(StockCycleRequest Request) : IRequest<StockCycleResult>;

public sealed class RunStockCycleHandler(IWarehouseStockClient stock, IProductLookupClient products) : IRequestHandler<RunStockCycleCommand, StockCycleResult>
{
    public async Task<StockCycleResult> Handle(RunStockCycleCommand command, CancellationToken ct)
    {
        var r = command.Request;
        var orderId = r.ProductionOrderId ?? Guid.NewGuid();
        var steps = new List<StockCycleStep>();
        var order = r.OrderNumber.Trim();

        bool Step(string name, bool ok, string detail) { steps.Add(new StockCycleStep(name, ok, detail)); return ok; }
        StockCycleResult Done() => new(steps.All(s => s.Ok), orderId, order, steps);
        string Key(string line) => $"{order}#{line}";

        try
        {
            // 0. the item flags come from Product (fail closed) and travel with every movement
            var info = await products.LookupAsync([r.ComponentProductId, r.FinishedProductId], ct);
            var component = info.FirstOrDefault(p => p.Id == r.ComponentProductId);
            var finished = info.FirstOrDefault(p => p.Id == r.FinishedProductId);
            if (!Step("product-flags", component != null && finished != null,
                    component == null || finished == null ? "A product is unknown to ProductService."
                        : $"component lot={component.IsLotControlled} serial={component.IsSerialControlled}; finished lot={finished.IsLotControlled} serial={finished.IsSerialControlled}"))
                return Done();
            var cLot = component!.IsLotControlled; var cSerial = component.IsSerialControlled;
            var fLot = finished!.IsLotControlled; var fSerial = finished.IsSerialControlled;

            // 1-2. what is there, and which lots to pick
            var availability = await stock.GetAvailabilityAsync(r.ComponentProductId, r.RawWarehouseId, ct);
            Step("availability", true, $"total {availability.TotalQuantity}, available {availability.AvailableQuantity}, cost {availability.UnitCost}, {availability.Balances.Count} balance rows in the raw warehouse");
            var suggestion = await stock.SuggestLotsAsync(r.ComponentProductId, r.ComponentQuantity, r.RawWarehouseId, ct);
            Step("suggest-lots", true, $"{suggestion.Lots.Count} lots: {string.Join(", ", suggestion.Lots.Select(l => $"{l.LotNumber}x{l.Take}"))}; shortfall {suggestion.Shortfall}");

            // 3. hold the components; asking again returns the same reservation
            var reservation = await stock.ReserveAsync(orderId, order, r.RawWarehouseId, [new StockReservationLine(r.ComponentProductId, r.ComponentQuantity, SourceLineId: orderId)], ct);
            if (!Step("reserve", reservation.Status == "Reserved" && !reservation.Replayed, $"reservation {reservation.ReservationId} {reservation.Status}")) return Done();
            var again = await stock.ReserveAsync(orderId, order, r.RawWarehouseId, [new StockReservationLine(r.ComponentProductId, r.ComponentQuantity, SourceLineId: orderId)], ct);
            if (!Step("reserve-replay", again.Replayed && again.ReservationId == reservation.ReservationId, $"replayed={again.Replayed}")) return Done();
            var firstLot = reservation.Lines.SelectMany(l => l.Lots).Select(l => (Guid?)l.LotId).FirstOrDefault();

            // 4. issue in two pieces against the reservation; the same key again is a replay, another quantity under the same key is refused
            StockMovementRequest Issue(string line, decimal quantity) => new("ProductionIssue", r.ComponentProductId, quantity, Key(line),
                WarehouseId: r.RawWarehouseId, ReservationId: reservation.ReservationId, SourceOrderId: orderId, RequiresLot: cLot, RequiresSerial: cSerial);
            var issue1 = await stock.RecordMovementAsync(Issue("1", r.FirstIssueQuantity), ct);
            if (!Step("issue-1", !issue1.Replayed, $"qty {issue1.Quantity}, cost {issue1.UnitCost}, value {issue1.ValueDelta}")) return Done();
            var replay = await stock.RecordMovementAsync(Issue("1", r.FirstIssueQuantity), ct);
            if (!Step("issue-1-replay", replay.Replayed && replay.MovementId == issue1.MovementId, $"replayed={replay.Replayed}")) return Done();
            try
            {
                await stock.RecordMovementAsync(Issue("1", r.FirstIssueQuantity + 1), ct);
                Step("issue-1-conflict", false, "A different quantity under the same key was accepted.");
                return Done();
            }
            catch (ConcurrencyException ex) when (ex.ErrorCode == "WH_IDEMPOTENCY_CONFLICT")
            {
                Step("issue-1-conflict", true, "refused with WH_IDEMPOTENCY_CONFLICT");
            }
            var issue2 = await stock.RecordMovementAsync(Issue("2", r.ComponentQuantity - r.FirstIssueQuantity), ct);
            Step("issue-2", !issue2.Replayed, $"qty {issue2.Quantity}, cost {issue2.UnitCost}, value {issue2.ValueDelta}");
            var fulfilled = await stock.GetReservationAsync(orderId, ct);
            if (!Step("reservation-fulfilled", fulfilled is { Status: "Fulfilled" }, $"status {fulfilled?.Status}")) return Done();

            // 5. give some back at the issue cost, scrap some
            var issueCost = issue1.UnitCost ?? availability.UnitCost;
            var back = await stock.RecordMovementAsync(new("ProductionReturn", r.ComponentProductId, r.ReturnQuantity, Key("R1"), WarehouseId: r.RawWarehouseId, LotId: firstLot,
                UnitCost: issueCost, SourceOrderId: orderId, RequiresLot: cLot, RequiresSerial: cSerial), ct);
            Step("return", !back.Replayed, $"qty {back.Quantity} at {back.UnitCost}, value {back.ValueDelta}");
            var scrap = await stock.RecordMovementAsync(new("ProductionScrap", r.ComponentProductId, r.ScrapQuantity, Key("S1"), WarehouseId: r.RawWarehouseId, LotId: firstLot,
                SourceOrderId: orderId, RequiresLot: cLot, RequiresSerial: cSerial), ct);
            Step("scrap", !scrap.Replayed, $"qty {scrap.Quantity}, value {scrap.ValueDelta}");

            // 6. the finished good arrives under quality hold: it cannot be picked until it is released
            var actualCost = Math.Round(((issue1.ValueDelta ?? 0) + (issue2.ValueDelta ?? 0)) * -1 / Math.Max(r.FinishedQuantity, 0.000001m), 6);
            var lotNumber = $"FG-{order}";
            var receipt = await stock.RecordMovementAsync(new("ProductionReceipt", r.FinishedProductId, r.FinishedQuantity, Key("FG1"), WarehouseId: r.QuarantineWarehouseId,
                LotNumber: lotNumber, ManufactureDate: DateTime.UtcNow.Date, UnitCost: actualCost, InitialLotStatus: "QcHold", SourceOrderId: orderId,
                RequiresLot: fLot, RequiresSerial: fSerial), ct);
            if (!Step("receipt-qc-hold", receipt.LotId != null, $"lot {receipt.LotId}, unit cost {receipt.UnitCost}")) return Done();
            var fgLot = receipt.LotId!.Value;
            var held = await stock.SuggestLotsAsync(r.FinishedProductId, r.FinishedQuantity, r.QuarantineWarehouseId, ct);
            if (!Step("hold-not-allocatable", held.Lots.All(l => l.LotId != fgLot), $"{held.Lots.Count} lots suggested while on hold")) return Done();

            var release = await stock.RecordMovementAsync(new("QcRelease", r.FinishedProductId, 0m, Key("QC1"), LotId: fgLot, SourceOrderId: orderId, RequiresLot: fLot), ct);
            Step("qc-release", !release.Replayed, "released to Available");
            var free = await stock.SuggestLotsAsync(r.FinishedProductId, r.FinishedQuantity, r.QuarantineWarehouseId, ct);
            if (!Step("released-allocatable", free.Lots.Any(l => l.LotId == fgLot), $"{free.Lots.Count} lots suggested after release")) return Done();

            // 7. a failed re-inspection: block the lot, then release it again
            var blocked = await stock.SetLotStatusAsync(fgLot, "Blocked", ct);
            Step("block-lot", blocked.Status == "Blocked", blocked.Status);
            var rel2 = await stock.RecordMovementAsync(new("QcRelease", r.FinishedProductId, 0m, Key("QC2"), LotId: fgLot, SourceOrderId: orderId, RequiresLot: fLot), ct);
            Step("qc-release-after-block", !rel2.Replayed, "released to Available");

            // 8. reverse the receipt (an issue of the lot under a new key) and look at the end state
            var reverse = await stock.RecordMovementAsync(new("ProductionIssue", r.FinishedProductId, r.FinishedQuantity, Key("FG1-REV"), WarehouseId: r.QuarantineWarehouseId,
                LotId: fgLot, SourceOrderId: orderId, RequiresLot: fLot, RequiresSerial: fSerial, Reason: "reversal of " + Key("FG1")), ct);
            Step("reverse-receipt", !reverse.Replayed, $"value {reverse.ValueDelta}");
            // 9. refusals the Warehouse must give (none of them changes stock)
            async Task Expect(string name, StockMovementRequest probe, string code)
            {
                try
                {
                    await stock.RecordMovementAsync(probe, ct);
                    Step(name, false, $"accepted, expected {code}");
                }
                catch (BusinessRuleException ex) when (ex.RuleName == code)
                {
                    Step(name, true, $"refused with {code}");
                }
            }
            await Expect("refuse-legacy-type", new("Sold", r.ComponentProductId, 1m, Key("X1")), "WH_MOVEMENT_TYPE");
            await Expect("refuse-key-format", new("ProductionIssue", r.ComponentProductId, 1m, order, WarehouseId: r.RawWarehouseId, LotId: firstLot, RequiresLot: cLot), "WH_REFERENCE_FORMAT");
            if (fLot)
                await Expect("refuse-lot-missing", new("ProductionReceipt", r.FinishedProductId, 1m, Key("X2"), WarehouseId: r.QuarantineWarehouseId, UnitCost: actualCost, RequiresLot: true), "WH_LOT_REQUIRED");

            var endRaw = await stock.GetAvailabilityAsync(r.ComponentProductId, null, ct);
            var endFg = await stock.GetAvailabilityAsync(r.FinishedProductId, null, ct);
            Step("end-state", true, $"component total {endRaw.TotalQuantity} (reserved {endRaw.ReservedQuantity}), finished total {endFg.TotalQuantity}");
        }
        catch (Exception ex) when (ex is BusinessRuleException or ConcurrencyException or EntityNotFoundException)
        {
            var code = ex is BusinessRuleException b ? b.RuleName : ex is ConcurrencyException c ? c.ErrorCode : null;
            Step("stopped", false, $"{code}: {ex.Message}");
        }

        return Done();
    }
}
