using ERP.NTF.Application.DTOs.Settings;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities.Settings;
using ERP.NTF.Infrastructure.Persistence;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — TwilioSmsSender still reads Twilio:* from
/// appsettings.json, not from these DB-backed settings. ApiKey is encrypted via
/// ISecretProtector before storage and never returned to callers.
/// </summary>
public class SmsSettingsService : ISmsSettingsService
{
    private readonly NotificationDbContext _dbContext;
    private readonly ISecretProtector _secretProtector;

    public SmsSettingsService(NotificationDbContext dbContext, ISecretProtector secretProtector)
    {
        _dbContext = dbContext;
        _secretProtector = secretProtector;
    }

    public async Task<SystemSmsSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.CustomSmsGatewayAllowed);
    }

    public async Task<SystemSmsSettingsDto> UpdateSystemSettingsAsync(UpdateSystemSmsSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomSmsGatewayAllowed);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.CustomSmsGatewayAllowed);
    }

    public async Task<TenantSmsSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<TenantSmsSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantSmsSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        var encryptedKey = string.IsNullOrEmpty(request.ApiKey) ? null : _secretProtector.Protect(request.ApiKey);
        settings.Update(request.ProviderName, encryptedKey, request.FromNumber);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private async Task<SystemSmsSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemSmsSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemSmsSettings.CreateDefault();
        _dbContext.Set<SystemSmsSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantSmsSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantSmsSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantSmsSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantSmsSettings>().Add(settings);
        return settings;
    }

    private static TenantSmsSettingsDto ToDto(TenantSmsSettings s) =>
        new(s.TenantId, s.ProviderName, !string.IsNullOrEmpty(s.ApiKeyEncrypted), s.FromNumber);
}
