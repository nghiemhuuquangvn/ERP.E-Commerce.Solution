using ERP.SHP.Application.DTOs.Settings;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities.Settings;
using ERP.SHP.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into shipping runtime behavior (carrier
/// selection, fee calculation). Mirrors IdentityService's SettingsService
/// lazy-create-on-read pattern, talking to ShippingDbContext directly rather than
/// through a dedicated repository layer.
/// </summary>
public class ShippingSettingsService : IShippingSettingsService
{
    private const string SystemCacheKey = "settings:shipping:shipping:system";

    private readonly ShippingDbContext _dbContext;
    private readonly ICacheService _cache;

    public ShippingSettingsService(ShippingDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public Task<SystemShippingSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemShippingSettingsDto(settings.CustomCarriersAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemShippingSettingsDto> UpdateSystemSettingsAsync(UpdateSystemShippingSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomCarriersAllowed);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "ShippingService", Category = "Shipping", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return new(settings.CustomCarriersAllowed);
    }

    public Task<TenantShippingSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:shipping:shipping:tenant:{tenantId}";
        return _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantShippingSettingsDto(settings.TenantId, settings.DefaultShippingFee, settings.FreeShippingThreshold);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantShippingSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantShippingSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.DefaultShippingFee, request.FreeShippingThreshold);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "ShippingService", Category = "Shipping", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"settings:shipping:shipping:tenant:{tenantId}", ct);
        return new(settings.TenantId, settings.DefaultShippingFee, settings.FreeShippingThreshold);
    }

    private async Task<SystemShippingSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemShippingSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemShippingSettings.CreateDefault();
        _dbContext.Set<SystemShippingSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantShippingSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantShippingSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantShippingSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantShippingSettings>().Add(settings);
        return settings;
    }
}
