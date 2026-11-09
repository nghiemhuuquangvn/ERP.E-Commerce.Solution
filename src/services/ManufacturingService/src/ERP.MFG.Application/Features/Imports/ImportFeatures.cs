using System.Globalization;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using MediatR;

namespace ERP.MFG.Application.Features.Imports;

// CSV import of a BOM or a routing (T2.9). A document is small, so unlike the product import this is NOT streamed and it is all-or-nothing:
// every row is checked with the same rules as typing it in, ALL problems are reported with their line number, and only a file without any problem
// (and not a dry run) creates a Draft. Parsing reuses CsvImportReader of ERP.Shared.Streaming (comma / semicolon / tab, UTF-8, headers without case).

public sealed record ImportRowError(int Line, string? Key, string Message);

/// <summary><c>CreatedId</c> is set only when the file was clean and it was not a dry run.</summary>
public sealed record ImportResult(bool DryRun, int RowCount, Guid? CreatedId, int? Version, IReadOnlyList<ImportRowError> Errors);

internal static class CsvCells
{
    public static readonly ImportLimits Limits = new() { MaxRows = 2000, MaxBytes = 5 * 1024 * 1024 };

    public static async Task<List<CsvImportRow>> ReadAllAsync(Stream body, string[] required, CancellationToken ct)
    {
        var rows = new List<CsvImportRow>();
        await foreach (var row in CsvImportReader.ReadAsync(body, required, Limits, new ImportBytesCounter(), ct)) rows.Add(row);
        if (rows.Count == 0) throw new BusinessRuleException("Import.NoRows", "The file has a header but no data rows.");
        return rows;
    }

    /// <summary>Invariant culture first; a lone comma as the decimal mark (Excel in a Vietnamese locale) is accepted too.</summary>
    public static decimal? Decimal(string text, out bool ok)
    {
        ok = true;
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (decimal.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)) return v;
        if (t.Count(c => c == ',') == 1 && !t.Contains('.') && decimal.TryParse(t.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v)) return v;
        ok = false;
        return null;
    }

    public static int? Integer(string text, out bool ok)
    {
        ok = true;
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        ok = false;
        return null;
    }

    public static bool Flag(string text, out bool ok)
    {
        ok = true;
        var t = text.Trim().ToLowerInvariant();
        if (t is "" or "0" or "false" or "no" or "n" or "khong" or "không") return false;
        if (t is "1" or "true" or "yes" or "y" or "x" or "co" or "có") return true;
        ok = false;
        return false;
    }

    public static TEnum? Enum<TEnum>(string text, out bool ok) where TEnum : struct, Enum
    {
        ok = true;
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (System.Enum.TryParse<TEnum>(text.Trim(), ignoreCase: true, out var v) && System.Enum.IsDefined(v)) return v;
        ok = false;
        return null;
    }
}

// ── BOM ──────────────────────────────────────────────────────────────────────────────────────────────────────

public static class BomCsvTemplate
{
    public const string Csv =
        "componentSku,quantity,uom,scrapPct,operationSeq,issueMethod,lineType,isPhantom,isFixedQuantity,alternativeGroup,alternativePriority,costAllocationPct,notes\n" +
        "NVL-VIT01,16,PCS,0,10,Backflush,Component,false,false,,,,\n" +
        "NVL-SON01,0.5,L,5,20,Manual,Component,false,false,,,,Two coats\n";
}

public sealed record ImportBomCommand(Guid PlantId, Guid ItemId, DateOnly? EffectiveFrom, decimal? BaseQuantity, decimal? YieldPct, bool DryRun, Stream Body) : IRequest<ImportResult>;

public sealed class ImportBomHandler(IPlantAccessService access, IBomRepository boms, IItemProfileRepository profiles, IProductLookupClient products, IUnitOfMeasureClient units,
    IBomSnapshotFactory snapshots, IMfgUnitOfWork uow) : IRequestHandler<ImportBomCommand, ImportResult>
{
    private sealed record Parsed(CsvImportRow Row, string Sku, string UomCode);

    public async Task<ImportResult> Handle(ImportBomCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        if (!item.IsActive) throw MfgRules.Invalid($"Item {item.Sku} is not active.");
        var profile = await profiles.GetAsync(r.ItemId, plant.Id, ct);
        if (profile is not null && profile.MakeOrBuy == MakeOrBuy.Buy) throw MfgRules.Fail(MfgErrors.Bom001);

        var rows = await CsvCells.ReadAllAsync(r.Body, ["componentsku", "quantity", "uom"], ct);
        var errors = new List<ImportRowError>();

        // one read per service for the whole file
        var skus = rows.Select(x => x.Get("componentsku")).Where(s => s.Length > 0).ToList();
        var bySku = (await products.LookupBySkuAsync(skus, ct)).GroupBy(p => p.Sku, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var byCode = (await units.LookupByCodeAsync(rows.Select(x => x.Get("uom")).Where(s => s.Length > 0).ToList(), ct))
            .GroupBy(u => u.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        // A code can match two units (a platform unit and the tenant's own): the one the component product itself uses wins, else the first.
        UnitInfo? PickUnit(string code, ProductInfo product)
            => byCode.TryGetValue(code, out var candidates) ? candidates.FirstOrDefault(u => u.Id == product.BaseUomId) ?? candidates[0] : null;

        var pairs = new List<(Guid, Guid)>();
        foreach (var row in rows)
            if (bySku.TryGetValue(row.Get("componentsku"), out var found) && found.Count == 1 && PickUnit(row.Get("uom"), found[0]) is { } unit) pairs.Add((found[0].Id, unit.Id));
        var resolved = pairs.Count == 0 ? new Dictionary<(Guid ItemId, Guid UomId), ComponentResolution>() : (await snapshots.ResolveComponentsAsync(pairs, ct)).ToDictionary(k => k.Key, k => k.Value);

        var version = await boms.MaxVersionAsync(r.ItemId, plant.Id, ct) + 1;
        var bom = Bom.Create(r.ItemId, plant.Id, item.Sku, item.Name, version, r.BaseQuantity ?? 1m, r.YieldPct ?? 100m, r.EffectiveFrom ?? BomScope.Today(), null, "Imported from CSV");

        foreach (var row in rows)
        {
            var sku = row.Get("componentsku");
            void Fail(string message) => errors.Add(new ImportRowError(row.Line, sku.Length > 0 ? sku : null, message));

            if (row.ParseError is not null) { Fail($"The line could not be read: {row.ParseError}"); continue; }
            if (sku.Length == 0) { Fail("componentSku is required."); continue; }
            if (!bySku.TryGetValue(sku, out var matches)) { Fail($"No item with SKU {sku}."); continue; }
            if (matches.Count > 1) { Fail($"SKU {sku} matches {matches.Count} items; it must be unique."); continue; }
            if (PickUnit(row.Get("uom"), matches[0]) is not { } unit) { Fail($"Unknown unit of measure '{row.Get("uom")}'."); continue; }
            if (!resolved.TryGetValue((matches[0].Id, unit.Id), out var resolution) || resolution.Component is null) { Fail(resolution?.Error ?? "The component could not be checked."); continue; }

            var quantity = CsvCells.Decimal(row.Get("quantity"), out var qOk);
            var scrap = CsvCells.Decimal(row.Get("scrappct"), out var sOk);
            var op = CsvCells.Integer(row.Get("operationseq"), out var oOk);
            var prio = CsvCells.Integer(row.Get("alternativepriority"), out var pOk);
            var alloc = CsvCells.Decimal(row.Get("costallocationpct"), out var aOk);
            var phantom = CsvCells.Flag(row.Get("isphantom"), out var phOk);
            var fixedQty = CsvCells.Flag(row.Get("isfixedquantity"), out var fxOk);
            var issue = CsvCells.Enum<IssueMethod>(row.Get("issuemethod"), out var iOk);
            var type = CsvCells.Enum<BomLineType>(row.Get("linetype"), out var tOk);
            if (!qOk || quantity is null) { Fail("quantity must be a number."); continue; }
            if (!sOk || !oOk || !pOk || !aOk) { Fail("scrapPct, operationSeq, alternativePriority and costAllocationPct must be numbers."); continue; }
            if (!phOk || !fxOk) { Fail("isPhantom and isFixedQuantity must be true or false."); continue; }
            if (!iOk) { Fail("issueMethod must be Manual or Backflush."); continue; }
            if (!tOk) { Fail("lineType must be Component, ByProduct or CoProduct."); continue; }

            var group = row.Get("alternativegroup");
            var notes = row.Get("notes");
            try
            {
                bom.AddLine(new BomLineValues(type ?? BomLineType.Component, resolution.Component.ItemId, resolution.Component.Code, resolution.Component.Name, quantity.Value,
                    unit.Id, scrap ?? 0m, fixedQty, phantom, issue ?? IssueMethod.Manual, op, group.Length == 0 ? null : group.ToUpperInvariant(),
                    group.Length == 0 ? null : prio, alloc, notes.Length == 0 ? null : notes));
            }
            catch (BusinessRuleException ex)
            {
                Fail(ex.Message);
            }
        }

        if (errors.Count > 0 || r.DryRun) return new ImportResult(r.DryRun, rows.Count, null, null, errors);

        boms.Add(bom);
        await uow.SaveChangesAsync(ct);
        return new ImportResult(false, rows.Count, bom.Id, bom.Version, errors);
    }
}

// ── Routing ──────────────────────────────────────────────────────────────────────────────────────────────────

public static class RoutingCsvTemplate
{
    public const string Csv =
        "operationSeq,name,workCenter,standardOperation,setupMinutes,runMinutes,basis,batchSize,queueMinutes,moveMinutes,isSubcontract,instructions\n" +
        "10,Assembly,WC-ASM,,15,10,PerUnit,,0,0,false,\n" +
        "20,Painting,WC-PNT,,30,6,PerUnit,,0,0,false,Two coats\n";
}

public sealed record ImportRoutingCommand(Guid PlantId, Guid ItemId, DateOnly? EffectiveFrom, bool DryRun, Stream Body) : IRequest<ImportResult>;

public sealed class ImportRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IProductLookupClient products, IPlantCatalog catalog, IMfgUnitOfWork uow)
    : IRequestHandler<ImportRoutingCommand, ImportResult>
{
    public async Task<ImportResult> Handle(ImportRoutingCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        if (!item.IsActive) throw MfgRules.Invalid($"Item {item.Sku} is not active.");

        var rows = await CsvCells.ReadAllAsync(r.Body, ["operationseq", "workcenter", "runminutes"], ct);
        var errors = new List<ImportRowError>();
        var centers = (await catalog.ListAsync<WorkCenter>(plant.Id, ct)).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var standards = (await catalog.ListAsync<StandardOperation>(plant.Id, ct)).ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        var version = await routings.MaxVersionAsync(r.ItemId, plant.Id, ct) + 1;
        var routing = Routing.Create(r.ItemId, plant.Id, item.Sku, item.Name, version, r.EffectiveFrom ?? RoutingScope.Today(), null, "Imported from CSV");

        foreach (var row in rows)
        {
            var seqText = row.Get("operationseq");
            void Fail(string message) => errors.Add(new ImportRowError(row.Line, seqText.Length > 0 ? seqText : null, message));

            if (row.ParseError is not null) { Fail($"The line could not be read: {row.ParseError}"); continue; }
            var seq = CsvCells.Integer(seqText, out var seqOk);
            if (!seqOk || seq is null) { Fail("operationSeq must be a whole number."); continue; }
            if (!centers.TryGetValue(row.Get("workcenter"), out var center)) { Fail($"Unknown work center '{row.Get("workcenter")}' in this plant."); continue; }

            StandardOperation? standard = null;
            var stdCode = row.Get("standardoperation");
            if (stdCode.Length > 0 && !standards.TryGetValue(stdCode, out standard)) { Fail($"Unknown standard operation '{stdCode}'."); continue; }

            var setup = CsvCells.Decimal(row.Get("setupminutes"), out var s1);
            var run = CsvCells.Decimal(row.Get("runminutes"), out var s2);
            var batch = CsvCells.Decimal(row.Get("batchsize"), out var s3);
            var queue = CsvCells.Integer(row.Get("queueminutes"), out var s4);
            var move = CsvCells.Integer(row.Get("moveminutes"), out var s5);
            var basis = CsvCells.Enum<RunTimeBasis>(row.Get("basis"), out var s6);
            var sub = CsvCells.Flag(row.Get("issubcontract"), out var s7);
            if (!s1 || !s2 || !s3 || !s4 || !s5) { Fail("setupMinutes, runMinutes, batchSize, queueMinutes and moveMinutes must be numbers."); continue; }
            if (!s6) { Fail("basis must be PerUnit or PerBatch."); continue; }
            if (!s7) { Fail("isSubcontract must be true or false."); continue; }

            try
            {
                var body = new RoutingOperationBody(seq, standard?.Id, row.Get("name"), center.Id, setup, run, basis, batch, queue, move, sub, row.Get("instructions"));
                routing.AddOperation(await RoutingScope.BuildAsync(body, routing, catalog, ct));
            }
            catch (BusinessRuleException ex)
            {
                Fail(ex.Message);
            }
        }

        if (errors.Count > 0 || r.DryRun) return new ImportResult(r.DryRun, rows.Count, null, null, errors);

        routings.Add(routing);
        await uow.SaveChangesAsync(ct);
        return new ImportResult(false, rows.Count, routing.Id, routing.Version, errors);
    }
}
