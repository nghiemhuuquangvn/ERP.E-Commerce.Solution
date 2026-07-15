using ERP.NTF.Application.DTOs.Settings;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities.Settings;
using ERP.NTF.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into notification dispatch (which channel
/// actually fires). Talks to NotificationDbContext directly (no repository/UnitOfWork
/// layer for Settings, matching this service's own lightweight style).
/// </summary>
public class NotificationSettingsService : INotificationSettingsService
{
    private readonly NotificationDbContext _dbContext;
    private readonly ICacheService _cache;

    public NotificationSettingsService(NotificationDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<SystemNotificationSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        const string cacheKey = "settings:notification:notification:system";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemNotificationSettingsDto(settings.EmailChannelAvailable, settings.SmsChannelAvailable, settings.PushChannelAvailable);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemNotificationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemNotificationSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.EmailChannelAvailable, request.SmsChannelAvailable, request.PushChannelAvailable);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "NotificationService", Category = "Notification", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);

        const string cacheKey = "settings:notification:notification:system";
        await _cache.RemoveAsync(cacheKey, ct);

        return new(settings.EmailChannelAvailable, settings.SmsChannelAvailable, settings.PushChannelAvailable);
    }

    public async Task<TenantNotificationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:notification:notification:tenant:{tenantId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantNotificationSettingsDto(settings.TenantId, settings.EmailEnabledByDefault, settings.SmsEnabledByDefault, settings.PushEnabledByDefault);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantNotificationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantNotificationSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.EmailEnabledByDefault, request.SmsEnabledByDefault, request.PushEnabledByDefault);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "NotificationService", Category = "Notification", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);

        var cacheKey = $"settings:notification:notification:tenant:{tenantId}";
        await _cache.RemoveAsync(cacheKey, ct);

        return new(settings.TenantId, settings.EmailEnabledByDefault, settings.SmsEnabledByDefault, settings.PushEnabledByDefault);
    }

    private async Task<SystemNotificationSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemNotificationSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemNotificationSettings.CreateDefault();
        _dbContext.Set<SystemNotificationSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantNotificationSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantNotificationSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantNotificationSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantNotificationSettings>().Add(settings);
        return settings;
    }
}
