using ERP.SHP.Application.DTOs.Settings;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using ERP.SHP.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Messaging;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>
/// System (platform) and Tenant (per-company) shipping settings, and the shop's accounts at the carriers (P1-11). Mirrors IdentityService's
/// SettingsService lazy-create-on-read pattern, talking to ShippingDbContext directly. Carrier secrets are encrypted with ISecretProtector and
/// never returned.
/// </summary>
public class ShippingSettingsService : IShippingSettingsService
{
    private const string SystemCacheKey = "settings:shipping:shipping:system";

    private readonly ShippingDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly ISecretProtector _secrets;
    private readonly ICarrierProviderResolver _carriers;

    public ShippingSettingsService(ShippingDbContext dbContext, ICacheService cache, ISecretProtector secrets, ICarrierProviderResolver carriers)
    {
        _dbContext = dbContext;
        _cache = cache;
        _secrets = secrets;
        _carriers = carriers;
    }

    private static SystemShippingSettingsDto ToDto(SystemShippingSettings s) =>
        new(s.CustomCarriersAllowed, s.GhtkAvailable, s.VtpAvailable, s.GhnAvailable, s.JntAvailable);

    private static TenantShippingSettingsDto ToDto(TenantShippingSettings s) =>
        new(s.TenantId, s.DefaultShippingFee, s.FreeShippingThreshold, s.SenderName, s.SenderPhone, s.SenderAddress, s.SenderWard, s.SenderDistrict, s.SenderProvince,
            s.DefaultItemWeightGrams);

    public Task<SystemShippingSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemShippingSettingsDto> UpdateSystemSettingsAsync(UpdateSystemShippingSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomCarriersAllowed);
        settings.UpdateCarriers(request.GhtkAvailable, request.VtpAvailable, request.GhnAvailable, request.JntAvailable);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "ShippingService", Category = "Shipping", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return ToDto(settings);
    }

    public Task<TenantShippingSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:shipping:shipping:tenant:{tenantId}";
        return _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantShippingSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantShippingSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.DefaultShippingFee, request.FreeShippingThreshold);
        try
        {
            settings.UpdateSender(request.SenderName, request.SenderPhone, request.SenderAddress, request.SenderWard, request.SenderDistrict, request.SenderProvince,
                request.DefaultItemWeightGrams);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException("Shipping", ex.Message);
        }

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "ShippingService", Category = "Shipping", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"settings:shipping:shipping:tenant:{tenantId}", ct);
        return ToDto(settings);
    }

    // ── the shop's carrier accounts ──────────────────────────────────────

    public async Task<IReadOnlyList<CarrierAccountDto>> GetCarrierAccountsAsync(Guid tenantId, CancellationToken ct)
    {
        var system = await _dbContext.SystemShippingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var rows = await _dbContext.TenantCarrierCredentials.AsNoTracking().Where(c => c.TenantId == tenantId).ToListAsync(ct);
        return CarrierCodes.All.Select(code => ToDto(code, rows.FirstOrDefault(r => r.CarrierCode == code), system)).ToList();
    }

    public async Task<CarrierAccountDto> UpdateCarrierAccountAsync(Guid tenantId, string carrierCode, UpdateCarrierAccountRequest request, CancellationToken ct)
    {
        var code = CarrierCodes.Normalize(carrierCode) ?? throw new BusinessRuleException("Shipping", $"'{carrierCode}' is not a carrier this platform integrates.");
        if (_carriers.Provider(code) is { Implemented: false })
            throw new BusinessRuleException("Shipping", $"{code} is not integrated yet and cannot be set up.");

        var row = await _dbContext.TenantCarrierCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CarrierCode == code, ct);
        if (row == null)
        {
            row = TenantCarrierCredential.Create(tenantId, code);
            _dbContext.TenantCarrierCredentials.Add(row);
        }

        row.Update(request.Enabled, request.Sandbox, request.PublicId, request.Username,
            string.IsNullOrEmpty(request.Secret) ? null : _secrets.Protect(request.Secret),
            string.IsNullOrEmpty(request.WebhookSecret) ? null : _secrets.Protect(request.WebhookSecret));

        // Switching an account on without everything it needs would only fail later, at the first order: say it now.
        if (row.Enabled && !CarrierProviderResolver.IsComplete(new CarrierCredentials(code, row.PublicId, row.Username,
                string.IsNullOrEmpty(row.Secret1Encrypted) ? null : "x", null, row.Sandbox)))
            throw new BusinessRuleException("Shipping", $"{code} cannot be enabled before its account is complete ({RequiredFields(code)}).");

        await _dbContext.SaveChangesAsync(ct);
        var system = await _dbContext.SystemShippingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return ToDto(code, row, system);
    }

    private CarrierAccountDto ToDto(string code, TenantCarrierCredential? row, SystemShippingSettings? system)
    {
        var integrated = _carriers.Provider(code)?.Implemented ?? false;
        var complete = row != null && CarrierProviderResolver.IsComplete(new CarrierCredentials(code, row.PublicId, row.Username,
            string.IsNullOrEmpty(row.Secret1Encrypted) ? null : "x", null, row.Sandbox));
        return new CarrierAccountDto(code, CarrierBookingService.NameOf(code), integrated, system?.IsAvailable(code) ?? true,
            row?.Enabled ?? false, row?.Sandbox ?? true, row?.PublicId, row?.Username,
            !string.IsNullOrEmpty(row?.Secret1Encrypted), !string.IsNullOrEmpty(row?.Secret2Encrypted), complete);
    }

    private static string RequiredFields(string code) => code switch
    {
        CarrierCodes.ViettelPost => "login name and password",
        CarrierCodes.Ghn => "shop id and token",
        _ => "partner code and API token"
    };

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
