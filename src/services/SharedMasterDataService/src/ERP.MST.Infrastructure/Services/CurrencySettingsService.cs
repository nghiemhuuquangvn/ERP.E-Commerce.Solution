using ERP.MST.Application.DTOs.Settings;
using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities.Settings;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into currency-conversion runtime
/// behavior. Mirrors IdentityService's SettingsService lazy-create-on-read pattern,
/// talking to MasterDataDbContext directly rather than through a dedicated
/// repository layer.
/// </summary>
public class CurrencySettingsService : ICurrencySettingsService
{
    private readonly MasterDataDbContext _dbContext;

    public CurrencySettingsService(MasterDataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemCurrencySettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.MultiCurrencyAllowed);
    }

    public async Task<SystemCurrencySettingsDto> UpdateSystemSettingsAsync(UpdateSystemCurrencySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.MultiCurrencyAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.MultiCurrencyAllowed);
    }

    public async Task<TenantCurrencySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.DefaultCurrencyCode, settings.ExchangeRateSource, settings.DecimalDigits);
    }

    public async Task<TenantCurrencySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCurrencySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.DefaultCurrencyCode, request.ExchangeRateSource, request.DecimalDigits);
        await _dbContext.SaveChangesAsync(ct);
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
