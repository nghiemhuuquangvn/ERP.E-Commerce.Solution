using ERP.SO.Application.DTOs.Settings;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into cart/checkout runtime behavior.
/// Mirrors IdentityService's SettingsService lazy-create-on-read pattern, talking to
/// SalesOrderDbContext directly rather than through a dedicated repository layer.
/// </summary>
public class CartCheckoutSettingsService : ICartCheckoutSettingsService
{
    private readonly SalesOrderDbContext _dbContext;

    public CartCheckoutSettingsService(SalesOrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemCartCheckoutSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.GuestCheckoutAllowed);
    }

    public async Task<SystemCartCheckoutSettingsDto> UpdateSystemSettingsAsync(UpdateSystemCartCheckoutSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.GuestCheckoutAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.GuestCheckoutAllowed);
    }

    public async Task<TenantCartCheckoutSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.CartHoldMinutes, settings.RequireRegistrationAtCheckout, settings.CheckoutSteps);
    }

    public async Task<TenantCartCheckoutSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCartCheckoutSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.CartHoldMinutes, request.RequireRegistrationAtCheckout, request.CheckoutSteps);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.CartHoldMinutes, settings.RequireRegistrationAtCheckout, settings.CheckoutSteps);
    }

    private async Task<SystemCartCheckoutSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemCartCheckoutSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemCartCheckoutSettings.CreateDefault();
        _dbContext.Set<SystemCartCheckoutSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantCartCheckoutSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantCartCheckoutSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantCartCheckoutSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantCartCheckoutSettings>().Add(settings);
        return settings;
    }
}

public class OrderSettingsService : IOrderSettingsService
{
    private readonly SalesOrderDbContext _dbContext;

    public OrderSettingsService(SalesOrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemOrderSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.CustomOrderNumberFormatAllowed);
    }

    public async Task<SystemOrderSettingsDto> UpdateSystemSettingsAsync(UpdateSystemOrderSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomOrderNumberFormatAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.CustomOrderNumberFormatAllowed);
    }

    public async Task<TenantOrderSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.OrderNumberPrefix, settings.AutoCancelUnpaidAfterHours, settings.AllowEditAfterSubmit);
    }

    public async Task<TenantOrderSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantOrderSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.OrderNumberPrefix, request.AutoCancelUnpaidAfterHours, request.AllowEditAfterSubmit);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.OrderNumberPrefix, settings.AutoCancelUnpaidAfterHours, settings.AllowEditAfterSubmit);
    }

    private async Task<SystemOrderSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemOrderSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemOrderSettings.CreateDefault();
        _dbContext.Set<SystemOrderSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantOrderSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantOrderSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantOrderSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantOrderSettings>().Add(settings);
        return settings;
    }
}
