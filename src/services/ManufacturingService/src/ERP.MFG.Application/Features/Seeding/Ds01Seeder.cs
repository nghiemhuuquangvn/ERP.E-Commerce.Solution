using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Application.Features.Calendars;
using ERP.MFG.Application.Features.Crews;
using ERP.MFG.Application.Features.ItemProfiles;
using ERP.MFG.Application.Features.Imports;
using ERP.MFG.Application.Features.Plants;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Application.Features.Settings;
using ERP.MFG.Application.Features.WorkCenters;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using MediatR;

namespace ERP.MFG.Application.Features.Seeding;

/// <summary>Unit-of-measure codes the demo BOM uses (the tenant's MasterData codes; defaults match the standard units).</summary>
public sealed record Ds01Units(string Piece = "PCS", string Cubic = "M3", string Liter = "L", string Kg = "KG", string Set = "SET");

public sealed record Ds01Warehouses(Guid Raw, Guid Finished, Guid Quarantine, Guid? Scrap);

/// <summary>
/// Creates the standard test data set DS-01 (spec 03 section 1) that Phase 0-2 can hold: plant, settings, shifts, calendar, work centers, machines,
/// item profiles, approved BOMs and routings. Everything goes through the normal commands, so every business rule is exercised and a re-run only
/// creates what is missing. Stock, the quality plan and orders arrive with later phases.
/// </summary>
public sealed record SeedDs01Command(string PlantCode, string SkuPrefix, Ds01Warehouses? Warehouses, Ds01Units? Units) : IRequest<Ds01SeedResult>;

public sealed record Ds01SeedResult(Guid PlantId, string PlantCode, IReadOnlyDictionary<string, int> Created, IReadOnlyDictionary<string, int> Skipped, IReadOnlyList<string> NotSeeded);

public sealed class SeedDs01Handler(IMediator mediator, IProductLookupClient products) : IRequestHandler<SeedDs01Command, Ds01SeedResult>
{
    private static readonly string[] ItemCodes =
        ["TP-BAN01", "BTP-MAT01", "BTP-CHAN01", "KIT-PKG01", "NVL-GO01", "NVL-VIT01", "NVL-SON01", "NVL-KEO01", "NVL-KEO02", "NVL-THUNG01", "NVL-XOP01", "PP-MUN01"];

    private readonly Dictionary<string, int> _created = new();
    private readonly Dictionary<string, int> _skipped = new();

    private void Done(string kind, bool created)
    {
        var d = created ? _created : _skipped;
        d[kind] = d.GetValueOrDefault(kind) + 1;
    }

    public async Task<Ds01SeedResult> Handle(SeedDs01Command r, CancellationToken ct)
    {
        var prefix = r.SkuPrefix ?? "";
        var units = r.Units ?? new Ds01Units();
        var plantCode = string.IsNullOrWhiteSpace(r.PlantCode) ? "PL01" : r.PlantCode.Trim();

        // 1. The twelve products must exist (they belong to ProductService).
        var skus = ItemCodes.Select(c => prefix + c).ToList();
        var found = (await products.LookupBySkuAsync(skus, ct)).GroupBy(p => p.Sku, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var missing = skus.Where(s => !found.ContainsKey(s)).ToList();
        if (missing.Count > 0) throw MfgRules.Invalid($"DS-01 needs these products first: {string.Join(", ", missing)}.");
        var ambiguous = skus.Where(s => found[s].Count > 1).ToList();
        if (ambiguous.Count > 0) throw MfgRules.Invalid($"More than one product has the SKU: {string.Join(", ", ambiguous)}.");
        Guid Item(string code) => found[prefix + code][0].Id;

        // 2. Plant (needs the warehouses only the first time).
        var plant = (await mediator.Send(new GetPlantsQuery(false), ct)).FirstOrDefault(p => p.Code.Equals(plantCode, StringComparison.OrdinalIgnoreCase));
        if (plant is null)
        {
            if (r.Warehouses is null) throw MfgRules.Invalid("Plant does not exist yet: give the raw, finished-goods and quarantine warehouse ids.");
            plant = await mediator.Send(new CreatePlantCommand(plantCode, "Nhà máy Bình Dương", "Asia/Ho_Chi_Minh", r.Warehouses.Raw, r.Warehouses.Finished, r.Warehouses.Quarantine, r.Warehouses.Scrap, null), ct);
            Done("plant", true);
        }
        else Done("plant", false);
        var pid = plant.Id;

        await mediator.Send(new UpdateSettingsCommand(pid, new ProductionSettingValues(5, 2, 3, false, false, true, true, IssueMethod.Manual, CostingMethod.Actual, 90, 20, ProductionSettingValues.DefaultLotFormat)), ct);

        // 3. Shifts and calendar.
        var shifts = (await mediator.Send(new GetShiftsQuery(pid), ct)).ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, from, to) in new[] { ("CA1", "Ca 1", 6, 14), ("CA2", "Ca 2", 14, 22), ("CA3", "Ca 3", 22, 6) })
        {
            if (shifts.ContainsKey(code)) { Done("shift", false); continue; }
            shifts[code] = await mediator.Send(new CreateShiftCommand(pid, code, name, new TimeOnly(from, 0), new TimeOnly(to, 0), 30), ct);
            Done("shift", true);
        }

        var calendar = (await mediator.Send(new GetCalendarsQuery(pid), ct)).FirstOrDefault(c => c.Code.Equals("CAL-5D", StringComparison.OrdinalIgnoreCase));
        if (calendar is null)
        {
            calendar = await mediator.Send(new CreateCalendarCommand(pid, "CAL-5D", "Lịch 5 ngày"), ct);
            var both = new List<Guid> { shifts["CA1"].Id, shifts["CA2"].Id };
            foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
                calendar = await mediator.Send(new SetCalendarDayCommand(calendar.Id, day, both), ct);
            calendar = await mediator.Send(new SetCalendarDayCommand(calendar.Id, DayOfWeek.Saturday, [shifts["CA1"].Id]), ct);
            calendar = await mediator.Send(new AddCalendarExceptionCommand(calendar.Id, new DateOnly(2027, 1, 1), CalendarExceptionType.Holiday, "Tết Dương lịch", null), ct);
            Done("calendar", true);
        }
        else Done("calendar", false);
        if (plant.DefaultCalendarId != calendar.Id)
            plant = await mediator.Send(new UpdatePlantCommand(pid, plant.Name, plant.TimeZoneId, plant.RawMaterialWarehouseId, plant.FinishedGoodsWarehouseId, plant.QuarantineWarehouseId, plant.ScrapWarehouseId, calendar.Id), ct);

        // 4. Work centers and machines.
        var centers = (await mediator.Send(new GetWorkCentersQuery(pid), ct)).ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, type, res, eff, util, labor, machine) in new[]
        {
            ("WC-CUT", "Cắt", WorkCenterType.Machine, 2, 100m, 90m, 60000m, 120000m),
            ("WC-ASM", "Lắp ráp", WorkCenterType.Labor, 3, 100m, 100m, 50000m, 20000m),
            ("WC-PNT", "Sơn", WorkCenterType.Machine, 1, 80m, 100m, 55000m, 150000m),
            ("WC-PKG", "Đóng gói", WorkCenterType.Labor, 2, 100m, 100m, 45000m, 10000m),
        })
        {
            if (centers.ContainsKey(code)) { Done("workCenter", false); continue; }
            centers[code] = await mediator.Send(new CreateWorkCenterCommand(pid, code, new WorkCenterValues(name, null, calendar.Id, type, res, eff, util, labor, machine, 0, 0, null)), ct);
            Done("workCenter", true);
        }

        var machines = (await mediator.Send(new GetMachinesQuery(pid), ct)).ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, wc, ideal, retired) in new[]
        {
            ("M-CUT-01", "Máy cắt 1", "WC-CUT", 60m, false), ("M-CUT-02", "Máy cắt 2", "WC-CUT", 60m, false),
            ("M-PNT-01", "Máy sơn 1", "WC-PNT", 120m, false), ("M-OLD-01", "Máy cũ", "WC-CUT", 60m, true),
        })
        {
            if (machines.ContainsKey(code)) { Done("machine", false); continue; }
            var m = await mediator.Send(new CreateMachineCommand(pid, code, name, centers[wc].Id, ideal, null), ct);
            if (retired) await mediator.Send(new SetMachineStatusCommand(m.Id, MachineStatus.Retired), ct);
            Done("machine", true);
        }

        await mediator.Send(new SeedReasonCodesCommand(), ct);

        // 5. Item profiles (an upsert, so safe to repeat).
        ItemProfileValues P(MakeOrBuy mb, int fixedLt, int buyLt, LotSizingPolicy pol, decimal? fixedQty = null, decimal? min = null, decimal? mult = null, decimal safety = 0,
            decimal lot = 1, IssueMethod? issue = null, bool fqc = false, int shelf = 0) => new(mb, fixedLt, buyLt, pol, fixedQty, min, mult, null, safety, lot, issue, fqc, null, shelf);
        var profiles = new (string Code, ItemProfileValues Values)[]
        {
            ("TP-BAN01", P(MakeOrBuy.Make, 2, 0, LotSizingPolicy.LotForLot, safety: 10, fqc: true)),
            ("BTP-MAT01", P(MakeOrBuy.Make, 1, 0, LotSizingPolicy.LotForLot, lot: 50)),
            ("BTP-CHAN01", P(MakeOrBuy.Make, 1, 0, LotSizingPolicy.FixedQty, fixedQty: 200)),
            ("KIT-PKG01", P(MakeOrBuy.Make, 0, 0, LotSizingPolicy.LotForLot)),
            ("NVL-GO01", P(MakeOrBuy.Buy, 0, 10, LotSizingPolicy.LotForLot)),
            ("NVL-VIT01", P(MakeOrBuy.Buy, 0, 7, LotSizingPolicy.MinMultiple, min: 1000, mult: 500, safety: 500, issue: IssueMethod.Backflush)),
            ("NVL-SON01", P(MakeOrBuy.Buy, 0, 5, LotSizingPolicy.LotForLot)),
            ("NVL-KEO01", P(MakeOrBuy.Buy, 0, 5, LotSizingPolicy.LotForLot)),
            ("NVL-KEO02", P(MakeOrBuy.Buy, 0, 5, LotSizingPolicy.LotForLot)),
            ("NVL-THUNG01", P(MakeOrBuy.Buy, 0, 3, LotSizingPolicy.LotForLot, issue: IssueMethod.Backflush)),
            ("NVL-XOP01", P(MakeOrBuy.Buy, 0, 3, LotSizingPolicy.LotForLot, issue: IssueMethod.Backflush)),
            ("PP-MUN01", P(MakeOrBuy.Buy, 0, 0, LotSizingPolicy.LotForLot)),
        };
        foreach (var (code, values) in profiles)
        {
            await mediator.Send(new UpsertItemProfileCommand(Item(code), pid, values), ct);
            Done("itemProfile", true);
        }

        // 6. BOMs and routings: written as CSV, checked and created by the import handlers, then approved.
        var since = new DateOnly(2026, 1, 1);
        string Bom(params string[] rows) => "componentSku,quantity,uom,scrapPct,operationSeq,issueMethod,lineType,isPhantom,isFixedQuantity,alternativeGroup,alternativePriority,costAllocationPct,notes\n" + string.Join("\n", rows) + "\n";
        string L(string sku, string qty, string uom, string scrap = "0", string op = "", string issue = "", string type = "Component", string phantom = "0", string group = "", string prio = "") =>
            $"{prefix}{sku},{qty},{uom},{scrap},{op},{issue},{type},{phantom},0,{group},{prio},,";
        var boms = new (string Parent, string Csv)[]
        {
            ("TP-BAN01", Bom(L("BTP-MAT01", "1", units.Piece, op: "10", issue: "Manual"), L("BTP-CHAN01", "4", units.Piece, op: "10", issue: "Manual"),
                L("NVL-VIT01", "16", units.Piece, op: "10", issue: "Backflush"), L("NVL-SON01", "0.5", units.Liter, "5", "20", "Manual"),
                L("NVL-KEO01", "0.1", units.Kg, op: "10", issue: "Manual", group: "G1", prio: "1"), L("NVL-KEO02", "0.12", units.Kg, op: "10", issue: "Manual", group: "G1", prio: "2"),
                L("KIT-PKG01", "1", units.Set, op: "30", phantom: "1"))),
            ("KIT-PKG01", Bom(L("NVL-THUNG01", "1", units.Piece, issue: "Backflush"), L("NVL-XOP01", "2", units.Piece, issue: "Backflush"))),
            ("BTP-MAT01", Bom(L("NVL-GO01", "0.02", units.Cubic, "10", "10", "Manual"), L("PP-MUN01", "0.5", units.Kg, type: "ByProduct"))),
            ("BTP-CHAN01", Bom(L("NVL-GO01", "0.005", units.Cubic, "10"))),
        };
        foreach (var (parent, csv) in boms)
        {
            var itemId = Item(parent);
            if ((await mediator.Send(new GetBomsQuery(pid, itemId, VersionStatus.Approved), ct)).Count > 0) { Done("bom", false); continue; }
            var res = await mediator.Send(new ImportBomCommand(pid, itemId, since, 1, 100, false, new MemoryStream(Encoding.UTF8.GetBytes(csv))), ct);
            if (res.CreatedId is null) throw MfgRules.Invalid($"BOM {parent}: {string.Join("; ", res.Errors.Select(e => $"line {e.Line}: {e.Message}"))}");
            await mediator.Send(new ApproveBomCommand(res.CreatedId.Value), ct);
            Done("bom", true);
        }

        const string rh = "operationSeq,name,workCenter,standardOperation,setupMinutes,runMinutes,basis,batchSize,queueMinutes,moveMinutes,isSubcontract,instructions\n";
        var routings = new (string Parent, string Csv)[]
        {
            ("TP-BAN01", rh + "10,Lắp ráp,WC-ASM,,15,10,PerUnit,,0,0,0,\n20,Sơn,WC-PNT,,30,6,PerUnit,,0,0,0,\n30,Đóng gói,WC-PKG,,0,2,PerUnit,,0,0,0,\n"),
            ("BTP-MAT01", rh + "10,Cắt,WC-CUT,,20,3,PerUnit,,0,0,0,\n"),
        };
        foreach (var (parent, csv) in routings)
        {
            var itemId = Item(parent);
            if ((await mediator.Send(new GetRoutingsQuery(pid, itemId, VersionStatus.Approved), ct)).Count > 0) { Done("routing", false); continue; }
            var res = await mediator.Send(new ImportRoutingCommand(pid, itemId, since, false, new MemoryStream(Encoding.UTF8.GetBytes(csv))), ct);
            if (res.CreatedId is null) throw MfgRules.Invalid($"Routing {parent}: {string.Join("; ", res.Errors.Select(e => $"line {e.Line}: {e.Message}"))}");
            await mediator.Send(new ApproveRoutingCommand(res.CreatedId.Value), ct);
            Done("routing", true);
        }

        return new Ds01SeedResult(pid, plant.Code, _created, _skipped,
            ["Opening stock (spec 1.7): entered in the Warehouse by scripts/seed-ds01-products.js --stock (stock is not Manufacturing data)","Quality plan QP-BAN01: Phase 4", "Production order MO-A: Phase 3"]);
    }
}
