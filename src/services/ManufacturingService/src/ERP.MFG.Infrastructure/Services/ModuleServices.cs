using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.MFG.Infrastructure.Services;

/// <summary>Settings of a plant through a 60 s cache keyed by tenant and plant; an update invalidates it on this instance (another replica may show the old value for up to 60 s).</summary>
public sealed class ProductionSettingProvider(IProductionSettingRepository repo, IMemoryCache cache, ITenantProvider tenant) : IProductionSettingProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private string? Key(Guid plantId) => tenant.CurrentTenantId is { } t ? $"mfg:settings:t:{t}:p:{plantId}" : null;

    public async Task<ProductionSettingValues> GetAsync(Guid plantId, CancellationToken ct = default)
    {
        var key = Key(plantId);
        if (key is not null && cache.TryGetValue(key, out ProductionSettingValues? hit) && hit is not null) return hit;

        var setting = await repo.GetByPlantAsync(plantId, ct) ?? throw new EntityNotFoundException(nameof(ProductionSetting), plantId);
        var values = setting.ToValues();
        if (key is not null) cache.Set(key, values, Ttl);
        return values;
    }

    public void Invalidate(Guid plantId)
    {
        if (Key(plantId) is { } key) cache.Remove(key);
    }
}

/// <summary>No production orders exist before Phase 3, so nothing blocks a costing-method change yet (BR-SET-03). Replaced by the order-backed guard then.</summary>
public sealed class NoOrdersYetGuard : IOpenOrderGuard
{
    public Task<bool> HasOpenOrdersAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(false);
}

/// <summary>BR-GEN-09. Holders of <c>manufacturing:settings:manage</c> see every plant; everybody else only the plants they were granted.</summary>
public sealed class PlantAccessService(IPlantRepository plants, IPlantAccessRepository access, IUserContextService user) : IPlantAccessService
{
    public async Task<IReadOnlyCollection<Guid>?> VisiblePlantIdsAsync(CancellationToken ct = default)
    {
        if (user.HasPermission(PermissionConstants.Manufacturing_SettingsManage)) return null;
        return Guid.TryParse(user.GetUserId(), out var userId) ? await access.PlantIdsOfUserAsync(userId, ct) : [];
    }

    public async Task<Plant> GetVisiblePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await plants.GetAsync(plantId, ct) ?? throw new EntityNotFoundException(nameof(Plant), plantId);
        var visible = await VisiblePlantIdsAsync(ct);
        if (visible is not null && !visible.Contains(plantId)) throw new EntityNotFoundException(nameof(Plant), plantId); // 404, not 403: the plant is not theirs to know about
        return plant;
    }
}
