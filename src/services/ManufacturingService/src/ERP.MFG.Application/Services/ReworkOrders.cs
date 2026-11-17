using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Services;

/// <summary>What the quality manager may choose for a rework order; everything else comes from the NCR line (item, quantity, lot).</summary>
public sealed record ReworkOptions(Guid? RoutingId, DateOnly? DueDate, int? Priority);

/// <summary>
/// T4.11, BR-RW-01 / 02: builds the rework order of a Rework disposition line. Same item as the non-conforming goods, quantity of the line, <c>SourceType = Ncr</c> (<c>SourceRefId</c> = the NCR),
/// <c>ParentOrderId</c> = the order that made the goods, no BOM, one material = the held lot. The order is left in Draft: it is released, issued, reported and received like any other order.
/// </summary>
public sealed class ReworkOrderFactory(OrderSnapshotBuilder builder, IProductionOrderRepository orders, IDocumentNumberService numbers, IPlantClock clock)
{
    public async Task<ProductionOrder> CreateAsync(Plant plant, NonConformance ncr, NcrDisposition line, ProductionOrder source, StockLotInfo lot, ReworkOptions options, string by, CancellationToken ct = default)
    {
        MfgRules.ThrowIf(!plant.IsActive, MfgErrors.Po006, ("code", plant.Code));
        var lotNumber = ncr.LotNumber ?? lot.LotNumber;
        var built = await builder.BuildReworkAsync(new ReworkBuildRequest(plant, ncr.ItemId, line.Qty, options.DueDate, options.RoutingId, source.OutputWarehouseId, lot.LotId, lotNumber,
            lot.WarehouseId ?? plant.QuarantineWarehouseId, clock.Today(plant)), ct);
        var orderNo = await numbers.NextAsync(DocumentTypes.ProductionOrder, Guid.Empty, clock.LocalNow(plant), ct);   // one counter per tenant and month: the number is also a Warehouse key

        var header = new OrderHeaderValues(plant.Id, ncr.ItemId, built.Item.Sku, built.Item.Name, line.Qty, built.UomId, null, built.RoutingId, options.Priority ?? 3,
            built.DueDate ?? clock.Today(plant), built.OutputWarehouseId, OrderType.Rework, OrderSourceType.Ncr, ncr.Id, source.Id,
            $"Rework of {ncr.NcrNo} line {line.LineNo} (lot {lotNumber})");
        var order = ProductionOrder.Create(orderNo, header, built.Snapshot, built.Schedule, by);
        orders.Add(order);
        return order;
    }
}
