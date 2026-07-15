using ERP.MST.Application.DTOs.Settings;
using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities.Settings;
using ERP.MST.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.MST.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into currency-conversion runtime
/// behavior. Mirrors IdentityService's SettingsService lazy-create-on-read pattern,
/// talking to MasterDataDbContext directly rather than through a dedicated
/// repository layer.
/// </summary>
public class CurrencySettingsService : ICurrencySettingsService
{
    private const string SystemCacheKey = "settings:sharedmasterdata:currency:system";

    private readonly MasterDataDbContext _dbContext;
    private readonly ICacheService _cache;

    public CurrencySettingsService(MasterDataDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public Task<SystemCurrencySettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemCurrencySettingsDto(settings.MultiCurrencyAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemCurrencySettingsDto> UpdateSystemSettingsAsync(UpdateSystemCurrencySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.MultiCurrencyAllowed);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "SharedMasterDataService", Category = "Currency", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return new(settings.MultiCurrencyAllowed);
    }

    public Task<TenantCurrencySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:sharedmasterdata:currency:tenant:{tenantId}";
        return _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantCurrencySettingsDto(settings.TenantId, settings.DefaultCurrencyCode, settings.ExchangeRateSource, settings.DecimalDigits);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantCurrencySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCurrencySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.DefaultCurrencyCode, request.ExchangeRateSource, request.DecimalDigits);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "SharedMasterDataService", Category = "Currency", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"settings:sharedmasterdata:currency:tenant:{tenantId}", ct);
        return new(settings.TenantId, settings.DefaultCurrencyCode, settings.ExchangeRateSource, settings.DecimalDigits);
    }

    private async Task<SystemCurrencySettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemCurrencySettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemCurrencySettings.CreateDefault();
        _dbContext.Set<SystemCurrencySettings>().Add(settings);
        return settings;
    }

    private async Task<TenantCurrencySettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantCurrencySettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantCurrencySettings.CreateDefault(tenantId);
        _dbContext.Set<TenantCurrencySettings>().Add(settings);
        return settings;
    }
}
