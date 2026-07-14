using ERP.NTF.Application.DTOs.Settings;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities.Settings;
using ERP.NTF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into notification dispatch (which channel
/// actually fires). Talks to NotificationDbContext directly (no repository/UnitOfWork
/// layer for Settings, matching this service's own lightweight style).
/// </summary>
public class NotificationSettingsService : INotificationSettingsService
{
    private readonly NotificationDbContext _dbContext;

    public NotificationSettingsService(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemNotificationSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.EmailChannelAvailable, settings.SmsChannelAvailable, settings.PushChannelAvailable);
    }

    public async Task<SystemNotificationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemNotificationSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.EmailChannelAvailable, request.SmsChannelAvailable, request.PushChannelAvailable);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.EmailChannelAvailable, settings.SmsChannelAvailable, settings.PushChannelAvailable);
    }

    public async Task<TenantNotificationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.TenantId, settings.EmailEnabledByDefault, settings.SmsEnabledByDefault, settings.PushEnabledByDefault);
    }

    public async Task<TenantNotificationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantNotificationSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.EmailEnabledByDefault, request.SmsEnabledByDefault, request.PushEnabledByDefault);
        await _dbContext.SaveChangesAsync(ct);
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
