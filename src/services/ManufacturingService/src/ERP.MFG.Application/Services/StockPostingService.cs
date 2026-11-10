using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace ERP.MFG.Application.Services;

/// <summary>What a Warehouse code means for a production order: the MFG_* code the caller sees, and whether the failure is a refusal at all.</summary>
public static class StockCodeMapper
{
    public const string IdempotencyConflict = "WH_IDEMPOTENCY_CONFLICT";

    /// <summary>The reservation moved under a reserved line (consumed by another document, released, replaced): the line is taken from free stock instead.</summary>
    public static bool IsReservationProblem(string code) => code is "WH_RESERVATION_NOT_OPEN" or "WH_RESERVATION_NOT_FOUND" or "WH_RESERVATION_LINE" or "WH_RESERVATION_EXCEEDED";

    /// <summary>The Warehouse could not be reached or kept answering a conflict: not a refusal, the document stays Pending.</summary>
    public static bool IsUnavailable(string code) => code is MfgErrors.Int001 or "WH_CONFLICT";

    /// <summary>The code shown to the user for a Warehouse refusal (BR-MAT-03/04/05); other codes pass through unchanged.</summary>
    public static string ToMfg(string warehouseCode, StockDocumentType type = StockDocumentType.Issue) => warehouseCode switch
    {
        "WH_INSUFFICIENT_STOCK" or "WH_INSUFFICIENT_IN_WAREHOUSE" => type is StockDocumentType.Backflush or StockDocumentType.Receipt ? MfgErrors.Mat008 : MfgErrors.Mat003,
        "WH_LOT_EXPIRED" or "WH_LOT_NOT_USABLE" or "WH_LOT_NOT_AVAILABLE" => MfgErrors.Mat005,
        "WH_LOT_REQUIRED" => MfgErrors.Mat004,
        "WH_SERIAL_NOT_SUPPORTED" => MfgErrors.Mat011,
        _ => warehouseCode,
    };
}

/// <summary>See <see cref="IStockPostingService"/>.</summary>
public sealed class StockPostingService(
    IWarehouseStockClient stock, IProductLookupClient products, IProductionOrderRepository orders, IMfgUnitOfWork uow, IPlantClock clock,
    IOrderReservationSync reservationSync, IOperationReportRepository reports, OperationLogCloser logCloser, IProductionSettingProvider settings, ILogger<StockPostingService> logger) : IStockPostingService
{
    public async Task<StockPostingResult> PostAsync(MaterialTransaction document, string by, CancellationToken ct = default)
    {
        if (document.Status != StockDocumentStatus.Pending) return Result(document);
        try
        {
            return document.Mode == StockDocumentMode.Rollback ? await RollbackAsync(document, by, ct) : await ForwardAsync(document, by, ct);
        }
        catch (BusinessRuleException ex) when (StockCodeMapper.IsUnavailable(ex.RuleName))
        {
            // the order or the product data could not be read: nothing is wrong with the document, try again later
            return await PendAsync(document, ex.Message, ct);
        }
        catch (ConcurrencyException ex)
        {
            // another request or the job changed the document / order at the same moment (the row version moved): it is theirs to finish; the stored state
            // is consistent and replays are free, so this round simply ends as Pending
            logger.LogInformation("Posting of {DocNo} met a concurrent change ({Message}); left to the next round", document.DocNo, ex.Message);
            return Result(document);
        }
    }

    // ── Forward: every line to the Warehouse, then the effect on the order ─────────────────────────────────────

    private async Task<StockPostingResult> ForwardAsync(MaterialTransaction document, string by, CancellationToken ct)
    {
        var order = await orders.GetAsync(document.OrderId, ct) ?? throw new EntityNotFoundException(nameof(ProductionOrder), document.OrderId);
        var flags = await ProductFlagsAsync(document, ct);

        foreach (var line in document.PendingLines.ToList())
        {
            var convertedOnce = false;
            while (true)
            {
                try
                {
                    var request = BuildRequest(document, line, order, flags[line.ItemId]);
                    var result = await stock.RecordMovementAsync(request, ct);
                    document.RecordLinePosted(line, result.MovementId, result.UnitCost, result.ValueDelta, result.LotId);
                    await uow.SaveChangesAsync(ct);                                  // progress is durable: a later retry replays instead of re-sending
                    break;
                }
                catch (BusinessRuleException ex) when (StockCodeMapper.IsUnavailable(ex.RuleName))
                {
                    return await PendAsync(document, ex.Message, ct);
                }
                catch (BusinessRuleException ex) when (line.IsFromReservation && !convertedOnce && StockCodeMapper.IsReservationProblem(ex.RuleName))
                {
                    // nothing was booked (the Warehouse looks at the key first): the reservation cannot cover this line any more, so take it from free stock
                    convertedOnce = true;
                    document.ConvertLineToFree(line);
                    await uow.SaveChangesAsync(ct);
                }
                catch (ConcurrencyException ex) when (ex.ErrorCode == StockCodeMapper.IdempotencyConflict)
                {
                    logger.LogError(ex, "Warehouse reports a key conflict for {Key} of {DocNo}: the line was booked with another quantity, warehouse or lot", line.MovementKey, document.DocNo);
                    return await RefuseAsync(document, line, StockCodeMapper.IdempotencyConflict, ex.Message, by, ct);
                }
                catch (ConcurrencyException ex)
                {
                    return await PendAsync(document, ex.Message, ct);
                }
                catch (BusinessRuleException ex)
                {
                    return await RefuseAsync(document, line, StockCodeMapper.ToMfg(ex.RuleName, document.Type), ex.Message, by, ct);
                }
            }
        }

        return await CompleteAsync(document, order, by, ct);
    }

    /// <summary>
    /// Every line is posted: the order takes the effect and the document becomes Posted in one save. If the order or a material was changed meanwhile the save
    /// is refused (row version); the document then stays Pending with all lines posted and the next round (the job, a fresh context) applies it - nothing is sent twice.
    /// </summary>
    private async Task<StockPostingResult> CompleteAsync(MaterialTransaction document, ProductionOrder order, string by, CancellationToken ct)
    {
        var now = clock.UtcNow;
        order.ApplyPostedDocument(document, by);
        if (document.Type == StockDocumentType.Backflush && document.SourceOperationReportId is { } reportId)
        {
            // the report counts only now that its materials are out of the Warehouse (ADR Phase 3, decision 3): both are applied in the same save
            var report = await reports.GetAsync(reportId, ct) ?? throw new EntityNotFoundException(nameof(OperationReport), reportId);
            if (report.Status == OperationReportStatus.Pending)
            {
                order.ApplyOperationReport(report, by, now);
                report.MarkPosted(now, by);
                await logCloser.CloseForCompletedAsync(order, ct);
            }
        }
        if (document.Type == StockDocumentType.Receipt)
        {
            // BR-PO-14a: receiving all of the quantity completes the order by itself; the operations still open are completed and their logs closed
            var setting = await settings.GetAsync(order.PlantId, ct);
            if (order.TryAutoComplete(setting.AutoCompleteOnFullReceipt, by, now)) await logCloser.CloseForCompletedAsync(order, ct);
        }
        document.MarkPosted(by, now);
        await uow.SaveChangesAsync(ct);

        await reservationSync.SyncAsync(order, ct);                                   // best effort: the flag stays set for the job if the Warehouse is not reachable
        return Result(document);
    }

    private async Task<StockPostingResult> PendAsync(MaterialTransaction document, string error, CancellationToken ct)
    {
        document.RecordTransientFailure(error, clock.UtcNow + StockRetryPolicy.Backoff(document.Attempts));
        await uow.SaveChangesAsync(ct);
        return Result(document);
    }

    private async Task<StockPostingResult> RefuseAsync(MaterialTransaction document, MaterialTransactionLine line, string code, string message, string by, CancellationToken ct)
    {
        document.RefuseLine(line, code, message);
        await uow.SaveChangesAsync(ct);
        return await RollbackAsync(document, by, ct);
    }

    // ── Rollback: reverse what was booked ──────────────────────────────────────────────────────────────────

    private async Task<StockPostingResult> RollbackAsync(MaterialTransaction document, string by, CancellationToken ct)
    {
        var order = await orders.GetAsync(document.OrderId, ct) ?? throw new EntityNotFoundException(nameof(ProductionOrder), document.OrderId);
        var flags = await ProductFlagsAsync(document, ct);

        foreach (var line in document.PostedLines.ToList())
        {
            try
            {
                var result = await stock.RecordMovementAsync(BuildReversal(document, line, order, flags[line.ItemId]), ct);
                document.RecordLineCompensated(line, result.MovementId);
                await uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is BusinessRuleException or ConcurrencyException)
            {
                // the reversal itself failed: the document stays Pending in Rollback mode so the job keeps trying; this is the alarm (LastError)
                logger.LogWarning(ex, "Reversal of {Key} of {DocNo} did not go through yet", line.CompensationKey, document.DocNo);
                return await PendAsync(document, "Rollback: " + ex.Message, ct);
            }
        }

        document.MarkFailed(by);
        if (document.Type == StockDocumentType.Backflush && document.SourceOperationReportId is { } reportId)
        {
            // nothing was applied: the report is kept for audit with the reason (a refused or abandoned backflush)
            var report = await reports.GetAsync(reportId, ct);
            if (report is { Status: OperationReportStatus.Pending })
                report.MarkFailed(document.LastErrorCode ?? "FAILED", document.LastError ?? "The backflush document failed.", by);
        }
        await uow.SaveChangesAsync(ct);
        return Result(document);
    }

    // ── Requests ────────────────────────────────────────────────────────────────────────────────────────────

    private static StockMovementRequest BuildRequest(MaterialTransaction document, MaterialTransactionLine line, ProductionOrder order, ProductInfo item)
    {
        if (line.Kind == StockLineKind.Receipt)
        {
            // the Warehouse creates the lot from its number (starting on quality hold when the item needs final inspection) and values the stock at the item cost
            return new("ProductionReceipt", line.ItemId, line.Qty, line.MovementKey, WarehouseId: line.WarehouseId, LotNumber: line.LotNumber, ExpiryDate: line.ExpiryDate,
                ManufactureDate: line.ManufactureDate, InitialLotStatus: line.IsQcHold ? "QcHold" : null, SourceOrderId: order.Id, RequiresLot: item.IsLotControlled, RequiresSerial: item.IsSerialControlled,
                ProductName: item.Name, ProductSku: item.Sku);
        }

        if (document.TakesStockOut(line))
        {
            // A reserved line names the open reservation (or, when none is open, the last one the order knew: a booking that was made before a lost answer is
            // found by its key first, so the id only matters for a first attempt; a stale one makes the Warehouse refuse and the line is taken from free stock).
            var reservationId = line.IsFromReservation ? order.ReservationId ?? Guid.NewGuid() : (Guid?)null;
            return new("ProductionIssue", line.ItemId, line.Qty, line.MovementKey, WarehouseId: line.WarehouseId, LotId: line.LotId, ReservationId: reservationId,
                SourceOrderId: order.Id, SourceLineId: line.IsFromReservation ? line.OrderMaterialId : null, RequiresLot: item.IsLotControlled, RequiresSerial: item.IsSerialControlled,
                AllowNegative: line.IsNegativeStock);
        }

        return new("ProductionReturn", line.ItemId, line.Qty, line.MovementKey, WarehouseId: line.WarehouseId, LotId: line.LotId, UnitCost: line.UnitCost,
            SourceOrderId: order.Id, RequiresLot: item.IsLotControlled, RequiresSerial: item.IsSerialControlled);
    }

    /// <summary>The opposite movement of a posted line, under the key of the line plus <c>R</c>, in the lot the Warehouse booked and at the cost it booked.</summary>
    private static StockMovementRequest BuildReversal(MaterialTransaction document, MaterialTransactionLine line, ProductionOrder order, ProductInfo item)
    {
        var lot = line.PostedLotId ?? line.LotId;
        var reason = $"Reversal of {line.MovementKey}";
        if (line.Kind == StockLineKind.Receipt)
        {
            // the lot the receipt created leaves again: scrap is allowed on a lot on quality hold, an issue is not; a line without a lot leaves the warehouse pool
            return line.PostedLotId is { } created
                ? new("ProductionScrap", line.ItemId, line.Qty, line.CompensationKey, WarehouseId: line.WarehouseId, LotId: created, SourceOrderId: order.Id, Reason: reason,
                    RequiresLot: item.IsLotControlled, RequiresSerial: item.IsSerialControlled)
                : new("ProductionIssue", line.ItemId, line.Qty, line.CompensationKey, WarehouseId: line.WarehouseId, SourceOrderId: order.Id, Reason: reason, RequiresSerial: item.IsSerialControlled);
        }

        return document.TakesStockOut(line)
            ? new("ProductionReturn", line.ItemId, line.Qty, line.CompensationKey, WarehouseId: line.WarehouseId, LotId: lot, UnitCost: line.UnitCost, SourceOrderId: order.Id,
                Reason: reason, RequiresLot: item.IsLotControlled, RequiresSerial: item.IsSerialControlled)
            : new("ProductionIssue", line.ItemId, line.Qty, line.CompensationKey, WarehouseId: line.WarehouseId, LotId: lot, SourceOrderId: order.Id,
                Reason: reason, RequiresLot: item.IsLotControlled, RequiresSerial: item.IsSerialControlled);
    }

    private async Task<Dictionary<Guid, ProductInfo>> ProductFlagsAsync(MaterialTransaction document, CancellationToken ct)
    {
        var ids = document.Lines.Select(l => l.ItemId).Distinct().ToList();
        var known = (await products.LookupAsync(ids, ct)).ToDictionary(p => p.Id);
        foreach (var id in ids)
            if (!known.ContainsKey(id)) throw MfgRules.Invalid($"Item {id} of {document.DocNo} does not exist any more.");
        return known;
    }

    private static StockPostingResult Result(MaterialTransaction document) => new(document.Status, document.LastErrorCode, document.LastError);
}
