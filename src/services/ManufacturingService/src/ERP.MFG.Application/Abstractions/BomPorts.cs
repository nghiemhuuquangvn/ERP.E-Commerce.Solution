using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

public interface IBomRepository
{
    /// <summary>A BOM with its lines (not deleted).</summary>
    Task<Bom?> GetAsync(Guid id, CancellationToken ct = default);

    Task<List<Bom>> ListAsync(Guid plantId, Guid? itemId, VersionStatus? status, CancellationToken ct = default);

    /// <summary>Every approved version of the plant, with lines (the graph of BR-BOM-04 / -13, where-used, explosion).</summary>
    Task<List<Bom>> ListApprovedAsync(Guid plantId, CancellationToken ct = default);

    /// <summary>Highest version number ever given to the item in the plant, deleted drafts included (the unique key still holds them).</summary>
    Task<int> MaxVersionAsync(Guid itemId, Guid plantId, CancellationToken ct = default);

    void Add(Bom bom);
}

/// <summary>The per-product unit conversions of ProductService (<c>internal/v1/product-uom-conversions/lookup</c>).</summary>
public interface IProductUomConversionClient
{
    Task<IReadOnlyList<ProductConversionFacts>> LookupAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
}

/// <summary>A BOM line's component after the checks of BR-BOM-01 and BR-BOM-07.</summary>
public sealed record ResolvedComponent(Guid ItemId, string Code, string Name, decimal FactorToBase, int BaseDecimals);

public interface IBomSnapshotFactory
{
    /// <summary>
    /// Turns BOMs into the numbers the calculators use: unit factor to the component's base unit and its decimals (BR-BOM-07; <c>MFG_BOM_006</c> when
    /// a unit cannot be converted). One call per service for the whole set, never one per line.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, BomSnapshot>> BuildAsync(IReadOnlyCollection<Bom> boms, CancellationToken ct = default);

    /// <summary>The component of a line being added: it exists and is active (<c>MFG_BOM_001</c>) and its unit converts to the item's base unit (<c>MFG_BOM_006</c>).</summary>
    Task<ResolvedComponent> ResolveComponentAsync(Guid componentItemId, Guid uomId, CancellationToken ct = default);
}

public interface IBomExplosionService
{
    /// <summary>The materials and outputs of an order (one level, phantoms passed through). <paramref name="bomId"/> null = the version effective on <paramref name="date"/>.</summary>
    Task<ExplosionResult> ExplodeAsync(Guid plantId, Guid itemId, Guid? bomId, decimal quantity, DateOnly date, CancellationToken ct = default);

    /// <summary>The whole tree, level by level.</summary>
    Task<IndentedNode> IndentedAsync(Guid plantId, Guid itemId, Guid? bomId, decimal quantity, DateOnly date, CancellationToken ct = default);
}

/// <summary>T2.3.1: the BOM that applies on a date (BR-BOM-03).</summary>
public interface IBomResolver
{
    Task<Bom?> GetEffectiveBomAsync(Guid itemId, Guid plantId, DateOnly date, CancellationToken ct = default);
}
