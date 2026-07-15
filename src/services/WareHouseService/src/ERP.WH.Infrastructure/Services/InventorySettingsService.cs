using ERP.WH.Application.DTOs.Settings;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Settings;
using ERP.WH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Newtonsoft.Json;

namespace ERP.WH.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into inventory runtime behavior (stock
/// negative-allowance checks, backorder logic). Mirrors IdentityService's
/// SettingsService lazy-create-on-read pattern, talking to WarehouseDbContext
/// directly rather than through a dedicated repository layer.
///
/// Reads are cached (15 min) via ICacheService; updates invalidate the cache and enqueue a
/// SettingsChangedIntegrationEvent onto the isolated Settings Outbox (published to Kafka by
/// SettingsOutboxProcessor/KafkaSettingsEventPublisher) — entirely separate from the
/// RabbitMQ-based IEventPublisher/EventPublisher flow used by inventory command handlers.
/// </summary>
public class InventorySettingsService : IInventorySettingsService
{
    private const string SystemCacheKey = "settings:warehouse:inventory:system";

    private readonly WarehouseDbContext _dbContext;
    private readonly ICacheService _cache;

    public InventorySettingsService(WarehouseDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public Task<SystemInventorySettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemInventorySettingsDto(settings.NegativeStockAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemInventorySettingsDto> UpdateSystemSettingsAsync(UpdateSystemInventorySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.NegativeStockAllowed);

        var @event = new SettingsChangedIntegrationEvent
        {
            ServiceName = "WareHouseService",
            Category = "Inventory",
            Scope = "System",
            TenantId = null
        };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return new(settings.NegativeStockAllowed);
    }

    public Task<TenantInventorySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = TenantCacheKey(tenantId);
        return _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantInventorySettingsDto(settings.TenantId, settings.LowStockThreshold, settings.BackorderAllowed, settings.DefaultHoldDays);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantInventorySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantInventorySettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.LowStockThreshold, request.BackorderAllowed, request.DefaultHoldDays);

        var @event = new SettingsChangedIntegrationEvent
        {
            ServiceName = "WareHouseService",
            Category = "Inventory",
            Scope = "Tenant",
            TenantId = tenantId
        };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
        return new(settings.TenantId, settings.LowStockThreshold, settings.BackorderAllowed, settings.DefaultHoldDays);
    }

    private static string TenantCacheKey(Guid tenantId) => $"settings:warehouse:inventory:tenant:{tenantId}";

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
