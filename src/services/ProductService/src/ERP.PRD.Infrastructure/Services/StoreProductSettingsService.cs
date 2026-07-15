using ERP.PRD.Application.DTOs.Settings;
using ERP.PRD.Application.Interfaces.Services;
using ERP.PRD.Domain.Entities.Settings;
using ERP.PRD.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.PRD.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — these settings are not yet wired into StoreProduct
/// runtime behavior (catalog rendering, tax display, review submission). Mirrors
/// IdentityService's SettingsService lazy-create-on-read pattern, simplified to talk
/// to ProductDbContext directly rather than through a dedicated repository layer.
/// Cache-aside (15-minute TTL) + SettingsChangedIntegrationEvent-on-update, mirroring
/// IdentityService's GeneralSettingsService/CompanySettingsService.
/// </summary>
public class StoreProductSettingsService : IStoreProductSettingsService
{
    private readonly ProductDbContext _dbContext;
    private readonly ICacheService _cache;
    private const string ServiceName = "ProductService";
    private const string Category = "StoreProduct";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public StoreProductSettingsService(ProductDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public Task<SystemStoreProductSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemSettingsAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, CacheTtl, ct);
    }

    public async Task<SystemStoreProductSettingsDto> UpdateSystemSettingsAsync(UpdateSystemStoreProductSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemSettingsAsync(ct);
        settings.Update(request.PriceDisplayIncludesTaxByDefault, request.ProductReviewsEnabled);
        AddSettingsChangedOutboxMessage("System", null);
        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return ToDto(settings);
    }

    public Task<TenantStoreProductSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
        {
            var settings = await GetOrCreateTenantSettingsAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, CacheTtl, ct);
    }

    public async Task<TenantStoreProductSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantStoreProductSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantSettingsAsync(tenantId, ct);
        settings.Update(request.StoreName, request.StoreDescription, request.LogoUrl, request.ShowOutOfStockProducts);
        AddSettingsChangedOutboxMessage("Tenant", tenantId);
        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
        return ToDto(settings);
    }

    private async Task<SystemStoreProductSettings> GetOrCreateSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemStoreProductSettings>().FirstOrDefaultAsync(ct);
        if (settings != null)
            return settings;

        settings = SystemStoreProductSettings.CreateDefault();
        _dbContext.Set<SystemStoreProductSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantStoreProductSettings> GetOrCreateTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantStoreProductSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null)
            return settings;

        settings = TenantStoreProductSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantStoreProductSettings>().Add(settings);
        return settings;
    }

    private static SystemStoreProductSettingsDto ToDto(SystemStoreProductSettings s) =>
        new(s.PriceDisplayIncludesTaxByDefault, s.ProductReviewsEnabled);

    private static TenantStoreProductSettingsDto ToDto(TenantStoreProductSettings s) =>
        new(s.TenantId, s.StoreName, s.StoreDescription, s.LogoUrl, s.ShowOutOfStockProducts);

    private void AddSettingsChangedOutboxMessage(string scope, Guid? tenantId)
    {
        var @event = new SettingsChangedIntegrationEvent { ServiceName = ServiceName, Category = Category, Scope = scope, TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));
    }

    private static string SystemCacheKey => "settings:product:store-product:system";
    private static string TenantCacheKey(Guid tenantId) => $"settings:product:store-product:tenant:{tenantId}";
}
