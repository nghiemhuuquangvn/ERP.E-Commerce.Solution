using ERP.NTF.Application.DTOs.Settings;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities.Settings;
using ERP.NTF.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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
    private readonly ICacheService _cache;

    public SmsSettingsService(NotificationDbContext dbContext, ISecretProtector secretProtector, ICacheService cache)
    {
        _dbContext = dbContext;
        _secretProtector = secretProtector;
        _cache = cache;
    }

    public async Task<SystemSmsSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        const string cacheKey = "settings:notification:sms:system";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemSmsSettingsDto(settings.CustomSmsGatewayAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemSmsSettingsDto> UpdateSystemSettingsAsync(UpdateSystemSmsSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomSmsGatewayAllowed);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "NotificationService", Category = "Sms", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);

        const string cacheKey = "settings:notification:sms:system";
        await _cache.RemoveAsync(cacheKey, ct);

        return new(settings.CustomSmsGatewayAllowed);
    }

    public async Task<TenantSmsSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:notification:sms:tenant:{tenantId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantSmsSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantSmsSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        var encryptedKey = string.IsNullOrEmpty(request.ApiKey) ? null : _secretProtector.Protect(request.ApiKey);
        settings.Update(request.ProviderName, encryptedKey, request.FromNumber);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "NotificationService", Category = "Sms", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);

        var cacheKey = $"settings:notification:sms:tenant:{tenantId}";
        await _cache.RemoveAsync(cacheKey, ct);

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
