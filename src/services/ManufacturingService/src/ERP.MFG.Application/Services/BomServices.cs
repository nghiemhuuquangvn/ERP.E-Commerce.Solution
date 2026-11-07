using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

public sealed class BomResolver(IBomRepository boms) : IBomResolver
{
    public async Task<Bom?> GetEffectiveBomAsync(Guid itemId, Guid plantId, DateOnly date, CancellationToken ct = default)
        => DocumentVersioning.Resolve((await boms.ListApprovedAsync(plantId, ct)).Where(b => b.ItemId == itemId), date);
}

/// <summary>Reads the products, units and conversions of a set of BOMs in three batched calls and fills the unit factors (BR-BOM-07).</summary>
public sealed class BomSnapshotFactory(IProductLookupClient products, IUnitOfMeasureClient units, IProductUomConversionClient conversions) : IBomSnapshotFactory
{
    private const int Chunk = 200;

    public async Task<IReadOnlyDictionary<Guid, BomSnapshot>> BuildAsync(IReadOnlyCollection<Bom> boms, CancellationToken ct = default)
    {
        var itemIds = boms.SelectMany(b => b.Lines.Select(l => l.ComponentItemId)).Distinct().ToList();
        var productMap = (await Batched<ProductInfo>(itemIds, products.LookupAsync, ct)).ToDictionary(p => p.Id);
        var conversionMap = (await Batched<ProductConversionFacts>(itemIds, conversions.LookupAsync, ct)).GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var unitIds = boms.SelectMany(b => b.Lines.Select(l => l.UomId)).Concat(productMap.Values.Where(p => p.BaseUomId is not null).Select(p => p.BaseUomId!.Value)).Distinct().ToList();
        var unitMap = (await Batched<UnitInfo>(unitIds, units.LookupAsync, ct)).Select(ToFacts).ToDictionary(u => u.Id);

        var result = new Dictionary<Guid, BomSnapshot>();
        foreach (var bom in boms)
        {
            var lines = new List<BomLineSnapshot>();
            foreach (var line in bom.Lines)
            {
                var (factor, decimals, name) = Convert(line.ComponentItemId, line.ComponentCode, line.UomId, productMap, unitMap, conversionMap);
                lines.Add(new BomLineSnapshot(line.LineNo, line.LineType, line.ComponentItemId, line.ComponentCode, name ?? line.ComponentName, line.Quantity, line.UomId,
                    factor, decimals, line.ScrapPct, line.IsFixedQuantity, line.IsPhantom, line.IssueMethod, line.OperationSeq, line.AlternativeGroup,
                    line.AlternativePriority, line.CostAllocationPct));
            }
            result[bom.Id] = new BomSnapshot(bom.Id, bom.ItemId, bom.ItemCode, bom.ItemName, bom.Version, bom.BaseQuantity, bom.YieldPct, lines);
        }
        return result;
    }

    public async Task<ResolvedComponent> ResolveComponentAsync(Guid componentItemId, Guid uomId, CancellationToken ct = default)
    {
        var product = (await products.LookupAsync([componentItemId], ct)).FirstOrDefault()
                      ?? throw MfgRules.Invalid($"Item {componentItemId} does not exist.");
        if (!product.IsActive) throw MfgRules.Fail(MfgErrors.Bom001); // BR-BOM-01: a component must be active
        var productMap = new Dictionary<Guid, ProductInfo> { [product.Id] = product };
        var unitIds = new[] { uomId, product.BaseUomId ?? Guid.Empty }.Where(i => i != Guid.Empty).Distinct().ToList();
        var unitMap = (await units.LookupAsync(unitIds, ct)).Select(ToFacts).ToDictionary(u => u.Id);
        var conversionMap = (await conversions.LookupAsync([product.Id], ct)).GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var (factor, decimals, _) = Convert(product.Id, product.Sku, uomId, productMap, unitMap, conversionMap);
        return new ResolvedComponent(product.Id, product.Sku, product.Name, factor, decimals);
    }

    public async Task<IReadOnlyDictionary<(Guid ItemId, Guid UomId), ComponentResolution>> ResolveComponentsAsync(IReadOnlyCollection<(Guid ItemId, Guid UomId)> pairs, CancellationToken ct = default)
    {
        var itemIds = pairs.Select(p => p.ItemId).Distinct().ToList();
        var productMap = (await Batched<ProductInfo>(itemIds, products.LookupAsync, ct)).ToDictionary(p => p.Id);
        var conversionMap = (await Batched<ProductConversionFacts>(itemIds, conversions.LookupAsync, ct)).GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var unitIds = pairs.Select(p => p.UomId).Concat(productMap.Values.Where(p => p.BaseUomId is not null).Select(p => p.BaseUomId!.Value)).Distinct().ToList();
        var unitMap = (await Batched<UnitInfo>(unitIds, units.LookupAsync, ct)).Select(ToFacts).ToDictionary(u => u.Id);

        var result = new Dictionary<(Guid, Guid), ComponentResolution>();
        foreach (var pair in pairs.Distinct())
        {
            try
            {
                if (!productMap.TryGetValue(pair.ItemId, out var product)) throw MfgRules.Invalid($"Item {pair.ItemId} does not exist.");
                if (!product.IsActive) throw MfgRules.Fail(MfgErrors.Bom001);
                var (factor, decimals, _) = Convert(product.Id, product.Sku, pair.UomId, productMap, unitMap, conversionMap);
                result[pair] = new ComponentResolution(new ResolvedComponent(product.Id, product.Sku, product.Name, factor, decimals), null);
            }
            catch (BusinessRuleException ex)
            {
                result[pair] = new ComponentResolution(null, ex.Message);
            }
        }
        return result;
    }

    private static (decimal Factor, int Decimals, string? Name) Convert(Guid itemId, string code, Guid lineUomId, IReadOnlyDictionary<Guid, ProductInfo> products,
        IReadOnlyDictionary<Guid, UnitFacts> units, IReadOnlyDictionary<Guid, List<ProductConversionFacts>> conversions)
    {
        if (!products.TryGetValue(itemId, out var product)) throw MfgRules.Invalid($"Item {code} no longer exists.");
        if (product.BaseUomId is not { } baseId || !units.TryGetValue(baseId, out var baseUnit)) throw MfgRules.Invalid($"Item {product.Sku} has no base unit of measure.");
        if (!units.TryGetValue(lineUomId, out var lineUnit)) throw MfgRules.Invalid("The unit of measure of the line does not exist.");

        var factor = UomRules.Factor(lineUnit, baseUnit, conversions.TryGetValue(itemId, out var own) ? own : [])
                     ?? throw MfgRules.Fail(MfgErrors.Bom006, ("from", lineUnit.Code), ("to", baseUnit.Code), ("item", product.Sku));
        return (factor, baseUnit.DecimalPlaces, product.Name);
    }

    private static async Task<List<T>> Batched<T>(IReadOnlyCollection<Guid> ids, Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyList<T>>> call, CancellationToken ct)
    {
        var all = new List<T>();
        foreach (var chunk in ids.Chunk(Chunk)) all.AddRange(await call(chunk, ct));
        return all;
    }

    private static UnitFacts ToFacts(UnitInfo u) => new(u.Id, u.Code, u.DecimalPlaces, u.BaseUnitId, u.ConversionFactor);
}

public sealed class BomExplosionService(IBomRepository boms, IBomSnapshotFactory snapshots, IProductionSettingProvider settings) : IBomExplosionService
{
    public async Task<ExplosionResult> ExplodeAsync(Guid plantId, Guid itemId, Guid? bomId, decimal quantity, DateOnly date, CancellationToken ct = default)
    {
        var (root, resolve, maxLevels) = await LoadAsync(plantId, itemId, bomId, date, followAll: false, ct);
        return BomExplosionCalculator.Explode(root, quantity, resolve, maxLevels);
    }

    public async Task<IndentedNode> IndentedAsync(Guid plantId, Guid itemId, Guid? bomId, decimal quantity, DateOnly date, CancellationToken ct = default)
    {
        var (root, resolve, maxLevels) = await LoadAsync(plantId, itemId, bomId, date, followAll: true, ct);
        return BomExplosionCalculator.ExplodeMultiLevel(root, quantity, resolve, maxLevels);
    }

    /// <summary>The root BOM plus the BOM effective on the date of every phantom (single level) or of every component (indented), found in one read of the approved versions.</summary>
    private async Task<(BomSnapshot Root, Func<Guid, BomSnapshot?> Resolve, int MaxLevels)> LoadAsync(Guid plantId, Guid itemId, Guid? bomId, DateOnly date, bool followAll, CancellationToken ct)
    {
        var maxLevels = (await settings.GetAsync(plantId, ct)).MaxBomLevels;
        var approved = (await boms.ListApprovedAsync(plantId, ct)).ToList();

        Bom rootBom;
        if (bomId is { } id)
        {
            rootBom = (approved.FirstOrDefault(b => b.Id == id) ?? await boms.GetAsync(id, ct)) ?? throw new EntityNotFoundException(nameof(Bom), id);
            if (rootBom.PlantId != plantId) throw new EntityNotFoundException(nameof(Bom), id);
        }
        else
        {
            rootBom = DocumentVersioning.Resolve(approved.Where(b => b.ItemId == itemId), date)
                      ?? throw MfgRules.Fail(MfgErrors.Po004, ("date", date.ToString("yyyy-MM-dd")));
        }

        var byItem = approved.GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => DocumentVersioning.Resolve(g, date));
        var needed = new Dictionary<Guid, Bom> { [rootBom.Id] = rootBom };
        var frontier = new List<Bom> { rootBom };
        for (var level = 1; level <= maxLevels && frontier.Count > 0; level++) // one level deeper than allowed is still read so the calculator can refuse it by name
        {
            var next = new List<Bom>();
            foreach (var bom in frontier)
                foreach (var line in bom.Lines.Where(l => l.LineType == BomLineType.Component && (followAll || l.IsPhantom)))
                    if (byItem.TryGetValue(line.ComponentItemId, out var sub) && sub is not null && needed.TryAdd(sub.Id, sub))
                        next.Add(sub);
            frontier = next;
        }

        var built = await snapshots.BuildAsync(needed.Values.ToList(), ct);
        var byComponent = needed.Values.Where(b => b.Id != rootBom.Id).GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => built[g.First().Id]);
        BomSnapshot? Resolve(Guid item) => byComponent.TryGetValue(item, out var s) ? s : null;
        return (built[rootBom.Id], Resolve, maxLevels);
    }
}
