using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;

namespace ERP.MFG.UnitTests.Common;

/// <summary>In-memory stand-ins for the persistence and client ports: the handlers run for real, only the storage and the other services are replaced.</summary>
public sealed class Fakes
{
    public readonly List<Plant> Plants = [];
    public readonly List<ProductionSetting> Settings = [];
    public readonly List<ProductionSettingAudit> Audits = [];
    public readonly List<ItemManufacturingProfile> Profiles = [];
    public readonly List<PlantUserAccess> Access = [];
    public int Saves;

    public readonly Dictionary<Guid, WarehouseInfo> Warehouses = [];
    public readonly Dictionary<Guid, ProductInfo> Products = [];
    public bool OpenOrders;
    public bool Unreachable;

    public IPlantRepository PlantRepo => new PlantRepo(this);
    public IProductionSettingRepository SettingRepo => new SettingRepo(this);
    public IItemProfileRepository ProfileRepo => new ProfileRepo(this);
    public IPlantAccessRepository AccessRepo => new AccessRepo(this);
    public IMfgUnitOfWork Uow => new Uow(this);
    public IWarehouseLookupClient WarehouseClient => new WarehouseClient(this);
    public IProductLookupClient ProductClient => new ProductClient(this);
    public IOpenOrderGuard Guard => new Guard(this);

    public Guid AddWarehouse(string code, bool active = true)
    {
        var id = Guid.NewGuid();
        Warehouses[id] = new WarehouseInfo(id, code, code, active);
        return id;
    }

    public ProductInfo AddProduct(string sku, bool active = true, bool withUom = true)
    {
        var p = new ProductInfo(Guid.NewGuid(), sku, "Product " + sku, active, withUom ? Guid.NewGuid() : null);
        Products[p.Id] = p;
        return p;
    }

    /// <summary>A plant with usable warehouses, its default settings, stored in the fake.</summary>
    public Plant AddPlant(string code = "PL01")
    {
        var plant = Plant.Create(code, "Plant " + code, null, AddWarehouse(code + "-RM"), AddWarehouse(code + "-FG"), AddWarehouse(code + "-QC"), null, null);
        Plants.Add(plant);
        Settings.Add(ProductionSetting.CreateDefault(plant.Id));
        return plant;
    }
}

internal sealed class PlantRepo(Fakes f) : IPlantRepository
{
    public Task<Plant?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Plants.FirstOrDefault(p => p.Id == id));
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default) => Task.FromResult(f.Plants.Any(p => p.Code == code && p.Id != exceptId));
    public Task<List<Plant>> ListAsync(IReadOnlyCollection<Guid>? onlyIds, bool activeOnly, CancellationToken ct = default)
        => Task.FromResult(f.Plants.Where(p => (onlyIds == null || onlyIds.Contains(p.Id)) && (!activeOnly || p.IsActive)).ToList());
    public void Add(Plant plant) => f.Plants.Add(plant);
}

internal sealed class SettingRepo(Fakes f) : IProductionSettingRepository
{
    public Task<ProductionSetting?> GetByPlantAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Settings.FirstOrDefault(s => s.PlantId == plantId));
    public Task<List<ProductionSettingAudit>> GetAuditAsync(Guid plantId, int take, CancellationToken ct = default)
        => Task.FromResult(f.Audits.Where(a => a.PlantId == plantId).OrderByDescending(a => a.ChangedAt).Take(take).ToList());
    public void Add(ProductionSetting setting) => f.Settings.Add(setting);
    public void AddAudit(ProductionSettingAudit audit) => f.Audits.Add(audit);
}

internal sealed class ProfileRepo(Fakes f) : IItemProfileRepository
{
    public Task<ItemManufacturingProfile?> GetAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => Task.FromResult(f.Profiles.FirstOrDefault(p => p.ItemId == itemId && p.PlantId == plantId));
    public Task<(List<ItemManufacturingProfile> Items, int Total)> ListAsync(Guid plantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var all = f.Profiles.Where(p => p.PlantId == plantId && (string.IsNullOrWhiteSpace(search) || p.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase))).OrderBy(p => p.ItemCode).ToList();
        return Task.FromResult((all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }
    public void Add(ItemManufacturingProfile profile) => f.Profiles.Add(profile);
}

internal sealed class AccessRepo(Fakes f) : IPlantAccessRepository
{
    public Task<List<Guid>> PlantIdsOfUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(f.Access.Where(a => a.UserId == userId).Select(a => a.PlantId).ToList());
    public Task<bool> ExistsAsync(Guid plantId, Guid userId, CancellationToken ct = default) => Task.FromResult(f.Access.Any(a => a.PlantId == plantId && a.UserId == userId));
    public Task<List<PlantUserAccess>> ListByPlantAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Access.Where(a => a.PlantId == plantId).ToList());
    public Task<PlantUserAccess?> GetAsync(Guid plantId, Guid userId, CancellationToken ct = default) => Task.FromResult(f.Access.FirstOrDefault(a => a.PlantId == plantId && a.UserId == userId));
    public void Add(PlantUserAccess access) => f.Access.Add(access);
    public void Remove(PlantUserAccess access) => f.Access.Remove(access);
}

internal sealed class Uow(Fakes f) : IMfgUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(++f.Saves);
}

internal sealed class WarehouseClient(Fakes f) : IWarehouseLookupClient
{
    public Task<IReadOnlyList<WarehouseInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => f.Unreachable
            ? throw ERP.MFG.Domain.Common.MfgRules.Fail(ERP.MFG.Domain.Common.MfgErrors.Int001, ("service", "warehouse"))
            : Task.FromResult<IReadOnlyList<WarehouseInfo>>(ids.Where(f.Warehouses.ContainsKey).Select(i => f.Warehouses[i]).ToList());
}

internal sealed class ProductClient(Fakes f) : IProductLookupClient
{
    public Task<IReadOnlyList<ProductInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => f.Unreachable
            ? throw ERP.MFG.Domain.Common.MfgRules.Fail(ERP.MFG.Domain.Common.MfgErrors.Int001, ("service", "product"))
            : Task.FromResult<IReadOnlyList<ProductInfo>>(ids.Where(f.Products.ContainsKey).Select(i => f.Products[i]).ToList());
}

internal sealed class Guard(Fakes f) : IOpenOrderGuard
{
    public Task<bool> HasOpenOrdersAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.OpenOrders);
}

/// <summary>A signed-in user with chosen permissions.</summary>
public sealed class FakeUser(Guid userId, params string[] permissions) : IUserContextService
{
    public UserContext GetCurrentUser() => new() { UserId = userId.ToString(), TenantId = Guid.NewGuid().ToString(), Permissions = permissions };
    public bool HasPermission(string permission) => permissions.Contains(permission);
    public bool HasRole(string role) => false;
    public string GetUserId() => userId.ToString();
    public string GetTenantId() => string.Empty;
}
