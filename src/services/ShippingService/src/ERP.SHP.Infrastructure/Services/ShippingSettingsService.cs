using ERP.SHP.Application.DTOs.Settings;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities.Settings;
using ERP.SHP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into shipping runtime behavior (carrier
/// selection, fee calculation). Mirrors IdentityService's SettingsService
/// lazy-create-on-read pattern, talking to ShippingDbContext directly rather than
/// through a dedicated repository layer.
/// </summary>
public class ShippingSettingsService : IShippingSettingsService
{
    private readonly ShippingDbContext _dbContext;

    public ShippingSettingsService(ShippingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemShippingSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.CustomCarriersAllowed);
    }

    public async Task<SystemShippingSettingsDto> UpdateSystemSettingsAsync(UpdateSystemShippingSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomCarriersAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.CustomCarriersAllowed);
    }

    public async Task<TenantShippingSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.DefaultShippingFee, settings.FreeShippingThreshold);
    }

    public async Task<TenantShippingSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantShippingSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.DefaultShippingFee, request.FreeShippingThreshold);
        await _dbContext.SaveChangesAsync(ct);
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
