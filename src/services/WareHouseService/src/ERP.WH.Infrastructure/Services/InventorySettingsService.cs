using ERP.WH.Application.DTOs.Settings;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Settings;
using ERP.WH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into inventory runtime behavior (stock
/// negative-allowance checks, backorder logic). Mirrors IdentityService's
/// SettingsService lazy-create-on-read pattern, talking to WarehouseDbContext
/// directly rather than through a dedicated repository layer.
/// </summary>
public class InventorySettingsService : IInventorySettingsService
{
    private readonly WarehouseDbContext _dbContext;

    public InventorySettingsService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemInventorySettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.NegativeStockAllowed);
    }

    public async Task<SystemInventorySettingsDto> UpdateSystemSettingsAsync(UpdateSystemInventorySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.NegativeStockAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.NegativeStockAllowed);
    }

    public async Task<TenantInventorySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.LowStockThreshold, settings.BackorderAllowed, settings.DefaultHoldDays);
    }

    public async Task<TenantInventorySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantInventorySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.LowStockThreshold, request.BackorderAllowed, request.DefaultHoldDays);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.LowStockThreshold, settings.BackorderAllowed, settings.DefaultHoldDays);
    }

    private async Task<SystemInventorySettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemInventorySettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemInventorySettings.CreateDefault();
        _dbContext.Set<SystemInventorySettings>().Add(settings);
        return settings;
    }

    private async Task<TenantInventorySettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantInventorySettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantInventorySettings.CreateDefault(tenantId);
        _dbContext.Set<TenantInventorySettings>().Add(settings);
        return settings;
    }
}
