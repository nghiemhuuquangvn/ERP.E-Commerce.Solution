using ERP.ACC.Application.DTOs.Settings;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities.Settings;
using ERP.ACC.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.ACC.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into invoicing/pricing runtime behavior.
/// Mirrors IdentityService's SettingsService lazy-create-on-read pattern, talking to
/// AccountingDbContext directly rather than through a dedicated repository layer.
/// Distinct from the existing TaxRate entity (a master-data list of tax rates) — this
/// is company-level policy configuration.
/// </summary>
public class TaxSettingsService : ITaxSettingsService
{
    private const string SystemCacheKey = "settings:accounting:tax:system";

    private readonly AccountingDbContext _dbContext;
    private readonly ICacheService _cache;

    public TaxSettingsService(AccountingDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public Task<SystemTaxSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemTaxSettingsDto(settings.RegionalTaxConfigAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemTaxSettingsDto> UpdateSystemSettingsAsync(UpdateSystemTaxSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.RegionalTaxConfigAllowed);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "AccountingService", Category = "Tax", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return new(settings.RegionalTaxConfigAllowed);
    }

    public Task<TenantTaxSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:accounting:tax:tenant:{tenantId}";
        return _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantTaxSettingsDto(settings.TenantId, settings.CompanyTaxCode, settings.DefaultTaxRatePercent, settings.PricesIncludeTax);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantTaxSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantTaxSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.CompanyTaxCode, request.DefaultTaxRatePercent, request.PricesIncludeTax);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "AccountingService", Category = "Tax", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"settings:accounting:tax:tenant:{tenantId}", ct);
        return new(settings.TenantId, settings.CompanyTaxCode, settings.DefaultTaxRatePercent, settings.PricesIncludeTax);
    }

    private async Task<SystemTaxSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemTaxSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemTaxSettings.CreateDefault();
        _dbContext.Set<SystemTaxSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantTaxSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantTaxSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantTaxSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantTaxSettings>().Add(settings);
        return settings;
    }
}
