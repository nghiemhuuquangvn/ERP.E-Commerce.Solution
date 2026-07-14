using ERP.PRD.Application.DTOs.Settings;
using ERP.PRD.Application.Interfaces.Services;
using ERP.PRD.Domain.Entities.Settings;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — these settings are not yet wired into StoreProduct
/// runtime behavior (catalog rendering, tax display, review submission). Mirrors
/// IdentityService's SettingsService lazy-create-on-read pattern, simplified to talk
/// to ProductDbContext directly rather than through a dedicated repository layer.
/// </summary>
public class StoreProductSettingsService : IStoreProductSettingsService
{
    private readonly ProductDbContext _dbContext;

    public StoreProductSettingsService(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemStoreProductSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemSettingsAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<SystemStoreProductSettingsDto> UpdateSystemSettingsAsync(UpdateSystemStoreProductSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemSettingsAsync(ct);
        settings.Update(request.PriceDisplayIncludesTaxByDefault, request.ProductReviewsEnabled);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<TenantStoreProductSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantSettingsAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<TenantStoreProductSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantStoreProductSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantSettingsAsync(tenantId, ct);
        settings.Update(request.StoreName, request.StoreDescription, request.LogoUrl, request.ShowOutOfStockProducts);
        await _dbContext.SaveChangesAsync(ct);
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
}
