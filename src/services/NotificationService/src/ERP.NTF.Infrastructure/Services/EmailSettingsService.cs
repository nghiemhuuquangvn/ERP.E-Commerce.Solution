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
/// Storage+CRUD only for now — SmtpEmailSender still reads Email:* from
/// appsettings.json, not from these DB-backed settings. SmtpPassword is encrypted via
/// ISecretProtector (reused from Phase 1's Payment Settings) before storage and never
/// returned to callers.
/// </summary>
public class EmailSettingsService : IEmailSettingsService
{
    private readonly NotificationDbContext _dbContext;
    private readonly ISecretProtector _secretProtector;
    private readonly ICacheService _cache;

    public EmailSettingsService(NotificationDbContext dbContext, ISecretProtector secretProtector, ICacheService cache)
    {
        _dbContext = dbContext;
        _secretProtector = secretProtector;
        _cache = cache;
    }

    public async Task<SystemEmailSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        const string cacheKey = "settings:notification:email:system";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemEmailSettingsDto(settings.CustomSmtpAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemEmailSettingsDto> UpdateSystemSettingsAsync(UpdateSystemEmailSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomSmtpAllowed);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "NotificationService", Category = "Email", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);

        const string cacheKey = "settings:notification:email:system";
        await _cache.RemoveAsync(cacheKey, ct);

        return new(settings.CustomSmtpAllowed);
    }

    public async Task<TenantEmailSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:notification:email:tenant:{tenantId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantEmailSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantEmailSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        var encryptedPassword = string.IsNullOrEmpty(request.SmtpPassword) ? null : _secretProtector.Protect(request.SmtpPassword);
        settings.Update(request.SmtpHost, request.SmtpPort, request.SmtpUsername, encryptedPassword, request.FromAddress, request.FromName);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "NotificationService", Category = "Email", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);

        var cacheKey = $"settings:notification:email:tenant:{tenantId}";
        await _cache.RemoveAsync(cacheKey, ct);

        return ToDto(settings);
    }

    private async Task<SystemEmailSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemEmailSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemEmailSettings.CreateDefault();
        _dbContext.Set<SystemEmailSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantEmailSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantEmailSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantEmailSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantEmailSettings>().Add(settings);
        return settings;
    }

    private static TenantEmailSettingsDto ToDto(TenantEmailSettings s) =>
        new(s.TenantId, s.SmtpHost, s.SmtpPort, s.SmtpUsername, !string.IsNullOrEmpty(s.SmtpPasswordEncrypted), s.FromAddress, s.FromName);
}
