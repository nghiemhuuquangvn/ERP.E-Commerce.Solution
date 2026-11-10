using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

// ── Persistence ports (implemented in Infrastructure with EF Core / PostgreSQL) ─────────────────────────────

public interface IPlantRepository
{
    Task<Plant?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string normalizedCode, Guid? exceptId, CancellationToken ct = default);
    Task<List<Plant>> ListAsync(IReadOnlyCollection<Guid>? onlyIds, bool activeOnly, CancellationToken ct = default);
    void Add(Plant plant);
}

public interface IProductionSettingRepository
{
    Task<ProductionSetting?> GetByPlantAsync(Guid plantId, CancellationToken ct = default);
    Task<List<ProductionSettingAudit>> GetAuditAsync(Guid plantId, int take, CancellationToken ct = default);
    void Add(ProductionSetting setting);
    void AddAudit(ProductionSettingAudit audit);
}

public interface IItemProfileRepository
{
    Task<ItemManufacturingProfile?> GetAsync(Guid itemId, Guid plantId, CancellationToken ct = default);
    Task<(List<ItemManufacturingProfile> Items, int Total)> ListAsync(Guid plantId, string? search, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Every profile of the plant, tracked (the low-level codes are rewritten when a BOM is approved or made obsolete).</summary>
    Task<List<ItemManufacturingProfile>> ListByPlantAsync(Guid plantId, CancellationToken ct = default);
    void Add(ItemManufacturingProfile profile);
}

public interface IPlantAccessRepository
{
    /// <summary>Ids of the plants a user was granted.</summary>
    Task<List<Guid>> PlantIdsOfUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid plantId, Guid userId, CancellationToken ct = default);
    Task<List<PlantUserAccess>> ListByPlantAsync(Guid plantId, CancellationToken ct = default);
    Task<PlantUserAccess?> GetAsync(Guid plantId, Guid userId, CancellationToken ct = default);
    void Add(PlantUserAccess access);
    void Remove(PlantUserAccess access);
}

/// <summary>One commit for everything a request changed (settings, audit rows, plant ... in one transaction).</summary>
public interface IMfgUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// ── Other services (internal REST, service token, fail closed) ──────────────────────────────────────────────

/// <summary>Product facts ManufacturingService needs (ProductService <c>internal/v1/product-lookup</c>).</summary>
public sealed record ProductInfo(Guid Id, string Sku, string Name, bool IsActive, Guid? BaseUomId, bool IsLotControlled = false, bool IsSerialControlled = false);

public interface IProductLookupClient
{
    /// <summary>The known products among <paramref name="ids"/>; an unknown id is missing from the list. Throws MFG_INT_001 when the service cannot be reached - never an empty answer.</summary>
    Task<IReadOnlyList<ProductInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>The products with these SKUs (any case). A SKU that matches two products comes back twice - the caller reports it as ambiguous. MFG_INT_001 when the service cannot be reached.</summary>
    Task<IReadOnlyList<ProductInfo>> LookupBySkuAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default);
}

/// <summary>A unit of measure (MasterData <c>internal/v1/units-of-measure</c>).</summary>
public sealed record UnitInfo(Guid Id, string Code, string Name, int DecimalPlaces, Guid? BaseUnitId, decimal ConversionFactor);

public sealed record ConvertedQuantity(decimal Quantity, int ToDecimalPlaces);

public interface IUnitOfMeasureClient
{
    Task<IReadOnlyList<UnitInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>The units with these codes (any case); unknown codes are missing.</summary>
    Task<IReadOnlyList<UnitInfo>> LookupByCodeAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default);

    /// <summary>The quantity expressed in another unit of the same base; null when a unit is unknown; MFG_INT_001 when the service cannot be reached.</summary>
    Task<ConvertedQuantity?> ConvertAsync(Guid fromUomId, Guid toUomId, decimal quantity, CancellationToken ct = default);
}

/// <summary>A warehouse (WareHouseService <c>internal/v1/warehouses/lookup</c>).</summary>
public sealed record WarehouseInfo(Guid Id, string Code, string Name, bool IsActive);

public interface IWarehouseLookupClient
{
    Task<IReadOnlyList<WarehouseInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

// ── Domain services of the module ───────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tells whether production orders that block a costing-method change exist (BR-SET-03): Released, InProgress or Completed and not closed.
/// Orders arrive in Phase 3; until then nothing is open.
/// </summary>
public interface IOpenOrderGuard
{
    Task<bool> HasOpenOrdersAsync(Guid plantId, CancellationToken ct = default);

    /// <summary>Are there operations of Released / InProgress / OnHold orders on the work center (BR-WC-03)?</summary>
    Task<bool> HasOpenOperationsAsync(Guid workCenterId, CancellationToken ct = default);

    /// <summary>Orders of the plant that are neither Closed nor Cancelled (Draft included), as a count and one example number (R219: such a plant cannot be deactivated).</summary>
    Task<(int Count, string? Example)> GetUnfinishedOrdersAsync(Guid plantId, CancellationToken ct = default);
}

/// <summary>The production parameters of a plant, cached for 60 s per tenant and plant; every service reads parameters through this (T0.4.1).</summary>
public interface IProductionSettingProvider
{
    Task<ProductionSettingValues> GetAsync(Guid plantId, CancellationToken ct = default);
    void Invalidate(Guid plantId);
}

/// <summary>BR-GEN-09: which plants the signed-in user may see.</summary>
public interface IPlantAccessService
{
    /// <summary>Null = every plant (the user holds <c>manufacturing:settings:manage</c>); otherwise only these plants.</summary>
    Task<IReadOnlyCollection<Guid>?> VisiblePlantIdsAsync(CancellationToken ct = default);

    /// <summary>Throws EntityNotFound (HTTP 404) when the plant does not exist or the user may not see it.</summary>
    Task<Plant> GetVisiblePlantAsync(Guid plantId, CancellationToken ct = default);
}
