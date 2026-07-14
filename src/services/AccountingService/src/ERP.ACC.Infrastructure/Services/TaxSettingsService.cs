using ERP.ACC.Application.DTOs.Settings;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities.Settings;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
    private readonly AccountingDbContext _dbContext;

    public TaxSettingsService(AccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemTaxSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.RegionalTaxConfigAllowed);
    }

    public async Task<SystemTaxSettingsDto> UpdateSystemSettingsAsync(UpdateSystemTaxSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.RegionalTaxConfigAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.RegionalTaxConfigAllowed);
    }

    public async Task<TenantTaxSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.CompanyTaxCode, settings.DefaultTaxRatePercent, settings.PricesIncludeTax);
    }

    public async Task<TenantTaxSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantTaxSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.CompanyTaxCode, request.DefaultTaxRatePercent, request.PricesIncludeTax);
        await _dbContext.SaveChangesAsync(ct);
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
