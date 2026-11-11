using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

/// <summary>What an order is built from. <c>AsOf</c> is the date the default BOM and routing are taken on (plant today for a new order, BR-PO-04).</summary>
public sealed record OrderBuildRequest(Plant Plant, Guid ItemId, decimal Quantity, DateOnly DueDate, Guid? BomId, Guid? RoutingId, Guid? OutputWarehouseId, DateOnly AsOf);

public sealed record OrderBuildResult(
    ProductInfo Item, Guid UomId, Guid BomId, Guid? RoutingId, Guid OutputWarehouseId, OrderSnapshot Snapshot, OrderScheduleValues Schedule, IReadOnlyList<string> Warnings);

/// <summary>
/// BR-PO-03 / -04 / -05 / -06: checks the item, picks the BOM and routing, explodes the BOM one level into materials and outputs, copies the routing into
/// operations and schedules the order. It reads other services (item, unit) and the engineering data, and writes nothing.
/// </summary>
/// <summary>The lines of a released order for a new quantity (see <see cref="OrderSnapshotBuilder.RecomputeForQuantityAsync"/>).</summary>
public sealed record QuantityRecompute(
    IReadOnlyDictionary<int, decimal> RequiredByLine, IReadOnlyDictionary<int, decimal> PlannedByOutputLine,
    IReadOnlyDictionary<int, (decimal Standard, int Calendar)> MinutesBySeq, OrderScheduleValues Schedule);

public sealed class OrderSnapshotBuilder(
    IProductLookupClient products,
    IUnitOfMeasureClient units,
    IItemProfileRepository profiles,
    IBomRepository boms,
    IBomResolver bomResolver,
    IBomExplosionService explosion,
    IRoutingRepository routings,
    IRoutingResolver routingResolver,
    IOperationTimeService operationTime,
    IWorkingTimeService workingTime,
    IPlantClock clock)
{
    public async Task<OrderBuildResult> BuildAsync(OrderBuildRequest r, CancellationToken ct = default)
    {
        var plant = r.Plant;
        MfgRules.ThrowIf(r.Quantity <= 0, MfgErrors.Po001);
        MfgRules.ThrowIf(r.DueDate < clock.Today(plant), MfgErrors.Po003);

        // ── the item: exists, active, a Make item, no serial control
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        MfgRules.ThrowIf(!item.IsActive, MfgErrors.Po006, ("code", item.Sku));
        MfgRules.ThrowIf(item.IsSerialControlled, MfgErrors.Mat011, ("item", item.Sku));
        var profile = await profiles.GetAsync(r.ItemId, plant.Id, ct);
        MfgRules.ThrowIf(profile is { MakeOrBuy: MakeOrBuy.Buy }, MfgErrors.Itm001, ("item", item.Sku));
        var uomId = item.BaseUomId ?? throw MfgRules.Invalid($"Item {item.Sku} has no base unit of measure.");

        // quantity: at most as many decimals as the item's unit allows (TC-PO-003)
        var unit = (await units.LookupAsync([uomId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"The unit of measure of item {item.Sku} does not exist.");
        if (Math.Round(r.Quantity, unit.DecimalPlaces) != r.Quantity)
            throw MfgRules.Invalid($"The quantity of item {item.Sku} can have at most {unit.DecimalPlaces} decimal places ({unit.Code}).");

        var outputWarehouse = r.OutputWarehouseId ?? plant.FinishedGoodsWarehouseId;
        if (outputWarehouse != plant.RawMaterialWarehouseId && outputWarehouse != plant.FinishedGoodsWarehouseId && outputWarehouse != plant.QuarantineWarehouseId && outputWarehouse != plant.ScrapWarehouseId)
            throw MfgRules.Invalid("The output warehouse must be one of the warehouses of the plant.");

        // ── BOM (BR-PO-04): a chosen version must be approved and effective, otherwise the one effective on the date
        var bom = await PickBomAsync(r, ct);

        // ── routing: optional (BR-RT-07)
        var routing = await PickRoutingAsync(r, ct);

        // ── materials and outputs: one level, phantoms passed through (BR-PO-05)
        var exploded = await explosion.ExplodeAsync(plant.Id, r.ItemId, bom.Id, r.Quantity, r.AsOf, ct);
        var ids = exploded.Requirements.Select(x => x.ComponentItemId).Concat(exploded.Outputs.Select(x => x.ItemId)).Distinct().ToList();
        var known = (await products.LookupAsync(ids, ct)).ToDictionary(p => p.Id);
        ProductInfo Component(Guid id, string code)
        {
            if (!known.TryGetValue(id, out var p)) throw MfgRules.Invalid($"Item {code} does not exist.");
            MfgRules.ThrowIf(!p.IsActive, MfgErrors.Po006, ("code", p.Sku));
            MfgRules.ThrowIf(p.IsSerialControlled, MfgErrors.Mat011, ("item", p.Sku));
            return p;
        }

        var routingSeqs = routing?.Operations.Select(o => o.OperationSeq).ToList();
        var warnings = new List<string>();
        var materials = new List<OrderMaterialValues>();
        var lineNo = 0;
        foreach (var line in exploded.Requirements)
        {
            var component = Component(line.ComponentItemId, line.ComponentCode);
            var (seq, warning) = BomRoutingCheck.AssignOperation(line.OperationSeq, routingSeqs);
            if (warning is not null) warnings.Add($"{line.ComponentCode}: {warning}");
            var scrap = bom.Lines.FirstOrDefault(l => l.LineNo == line.SourceLineNo)?.ScrapPct ?? 0m;
            lineNo += 10;
            materials.Add(new OrderMaterialValues(lineNo, line.ComponentItemId, line.ComponentCode, line.ComponentName,
                component.BaseUomId ?? throw MfgRules.Invalid($"Item {line.ComponentCode} has no base unit of measure."),
                seq, line.Quantity, scrap, line.IssueMethod, plant.RawMaterialWarehouseId, line.AlternativeGroup));
        }

        var outputs = new List<OrderOutputValues>();
        lineNo = 0;
        foreach (var o in exploded.Outputs)
        {
            var product = Component(o.ItemId, o.ItemCode);
            lineNo += 10;
            outputs.Add(new OrderOutputValues(lineNo, o.LineType, o.ItemId, o.ItemCode, o.ItemName,
                product.BaseUomId ?? throw MfgRules.Invalid($"Item {o.ItemCode} has no base unit of measure."), o.PlannedQuantity, o.CostAllocationPct));
        }

        // ── operations and the schedule (BR-PO-06)
        var operations = new List<OrderOperationValues>();
        IReadOnlyList<ScheduledOperationInput> inputs = [];
        if (routing is { Operations.Count: > 0 })
        {
            inputs = await operationTime.BuildInputsAsync(routing, r.Quantity, ct);
            foreach (var op in routing.Operations)
            {
                var input = inputs.First(i => i.OperationSeq == op.OperationSeq);
                operations.Add(new OrderOperationValues(op.OperationSeq, op.Name, op.WorkCenterId, op.WorkCenterCode, op.SetupTimeMinutes, op.RunTimeMinutes, op.RunTimeBasis,
                    op.BatchSize, op.QueueTimeMinutes, op.MoveTimeMinutes, op.IsSubcontract, input.StandardMinutes, OperationTimeCalculator.CalendarMinutes(input.StandardMinutes, input.EfficiencyPct), op.IpqcInspectionPlanId));
            }
        }

        var schedule = await ScheduleAsync(plant, inputs, r.DueDate, profile?.FixedLeadTimeDays ?? 0, ct);

        return new OrderBuildResult(item, uomId, bom.Id, routing?.Id, outputWarehouse, new OrderSnapshot(materials, operations, outputs), schedule, warnings);
    }

    /// <summary>BR-PO-06: the schedule of an order (UTC) from the operations' inputs, the due date and the item's fixed lead time.</summary>
    private async Task<OrderScheduleValues> ScheduleAsync(Plant plant, IReadOnlyList<ScheduledOperationInput> inputs, DateOnly dueDate, int fixedLeadTimeDays, CancellationToken ct)
    {
        var calendarId = plant.DefaultCalendarId ?? (inputs.Count > 0 ? inputs[0].CalendarId : throw MfgRules.Invalid("The plant has no default calendar: set one before creating orders without a routing."));
        var plantCalendar = inputs.FirstOrDefault(i => i.CalendarId == calendarId)?.Calendar ?? await workingTime.ForCalendarAsync(calendarId, ct);
        var local = OrderScheduleCalculator.Calculate(inputs, dueDate, clock.LocalNow(plant), fixedLeadTimeDays, plantCalendar);
        return new OrderScheduleValues(
            clock.ToUtc(plant, local.Start), clock.ToUtc(plant, local.End), local.IsLate,
            local.Operations.ToDictionary(k => k.Key, k => (clock.ToUtc(plant, k.Value.Start), clock.ToUtc(plant, k.Value.End))));
    }

    /// <summary>
    /// BR-PO-12: the lines of a released order for a new quantity, from the SAME BOM it was created with. Required quantities by material line (matched by
    /// component), planned quantities of the outputs, standard / calendar minutes of every operation from its own stored times, and the new schedule.
    /// </summary>
    public async Task<QuantityRecompute> RecomputeForQuantityAsync(Plant plant, ProductionOrder order, decimal newQuantity, CancellationToken ct = default)
    {
        MfgRules.ThrowIf(newQuantity <= 0, MfgErrors.Po001);
        var unit = (await units.LookupAsync([order.UomId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid("The unit of measure of the order does not exist.");
        if (Math.Round(newQuantity, unit.DecimalPlaces) != newQuantity)
            throw MfgRules.Invalid($"The quantity of item {order.ItemCode} can have at most {unit.DecimalPlaces} decimal places ({unit.Code}).");

        var exploded = await explosion.ExplodeAsync(plant.Id, order.ItemId, order.BomId, newQuantity, clock.Today(plant), ct);

        var required = new Dictionary<int, decimal>();
        var pool = exploded.Requirements.ToList();
        foreach (var material in order.Materials.Where(m => !m.IsSubstitute && !m.IsUnplanned))
        {
            var match = pool.FirstOrDefault(x => x.ComponentItemId == material.ComponentItemId);
            if (match is null) continue;
            pool.Remove(match);
            required[material.LineNo] = match.Quantity;
        }

        var planned = new Dictionary<int, decimal>();
        var outputs = exploded.Outputs.ToList();
        foreach (var output in order.Outputs)
        {
            var match = outputs.FirstOrDefault(x => x.ItemId == output.ItemId && x.LineType == output.LineType);
            if (match is null) continue;
            outputs.Remove(match);
            planned[output.LineNo] = match.PlannedQuantity;
        }

        var sources = order.Operations.Select(o => new OperationTimingSource(o.OperationSeq, o.Name, o.WorkCenterId, o.SetupTimeMinutes, o.RunTimeMinutes, o.RunTimeBasis, o.BatchSize,
            o.QueueTimeMinutes, o.MoveTimeMinutes)).ToList();
        IReadOnlyList<ScheduledOperationInput> inputs = sources.Count > 0 ? await operationTime.BuildInputsAsync(sources, newQuantity, ct) : [];
        var minutes = inputs.ToDictionary(i => i.OperationSeq, i => (i.StandardMinutes, OperationTimeCalculator.CalendarMinutes(i.StandardMinutes, i.EfficiencyPct)));

        var profile = await profiles.GetAsync(order.ItemId, plant.Id, ct);
        var schedule = await ScheduleAsync(plant, inputs, order.DueDate, profile?.FixedLeadTimeDays ?? 0, ct);
        return new QuantityRecompute(required, planned, minutes, schedule);
    }

    private async Task<Bom> PickBomAsync(OrderBuildRequest r, CancellationToken ct)
    {
        if (r.BomId is { } id)
        {
            var chosen = await boms.GetAsync(id, ct);
            if (chosen is null || chosen.PlantId != r.Plant.Id || chosen.ItemId != r.ItemId) throw new EntityNotFoundException(nameof(Bom), id);
            MfgRules.ThrowIf(chosen.Status != VersionStatus.Approved || !chosen.IsEffectiveOn(r.AsOf), MfgErrors.Po005);
            return chosen;
        }
        return await bomResolver.GetEffectiveBomAsync(r.ItemId, r.Plant.Id, r.AsOf, ct)
               ?? throw MfgRules.Fail(MfgErrors.Po004, ("date", r.AsOf.ToString("yyyy-MM-dd")));
    }

    private async Task<Routing?> PickRoutingAsync(OrderBuildRequest r, CancellationToken ct)
    {
        if (r.RoutingId is { } id)
        {
            var chosen = await routings.GetAsync(id, ct);
            if (chosen is null || chosen.PlantId != r.Plant.Id || chosen.ItemId != r.ItemId) throw new EntityNotFoundException(nameof(Routing), id);
            MfgRules.ThrowIf(chosen.Status != VersionStatus.Approved || !chosen.IsEffectiveOn(r.AsOf), MfgErrors.Po005);
            return chosen;
        }
        return await routingResolver.GetEffectiveRoutingAsync(r.ItemId, r.Plant.Id, r.AsOf, ct);
    }
}
