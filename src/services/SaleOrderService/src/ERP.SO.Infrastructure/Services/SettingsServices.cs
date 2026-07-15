using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using ERP.SO.Application.DTOs.Settings;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.SO.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into cart/checkout runtime behavior.
/// Mirrors IdentityService's SettingsService lazy-create-on-read pattern, talking to
/// SalesOrderDbContext directly rather than through a dedicated repository layer.
/// Reads are cached (15 min) via <see cref="ICacheService"/>; updates write a
/// SettingsChangedIntegrationEvent to the Outbox (isolated Kafka publish path — see
/// SettingsOutboxProcessor/KafkaSettingsEventPublisher, unrelated to RabbitMqMessageBus)
/// and invalidate the cache entry after the update is durably saved.
/// </summary>
public class CartCheckoutSettingsService : ICartCheckoutSettingsService
{
    private const string SystemCacheKey = "settings:saleorder:cart-checkout:system";
    private static string TenantCacheKey(Guid tenantId) => $"settings:saleorder:cart-checkout:tenant:{tenantId}";

    private readonly SalesOrderDbContext _dbContext;
    private readonly ICacheService _cache;

    public CartCheckoutSettingsService(SalesOrderDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<SystemCartCheckoutSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return await _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemCartCheckoutSettingsDto(settings.GuestCheckoutAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemCartCheckoutSettingsDto> UpdateSystemSettingsAsync(UpdateSystemCartCheckoutSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.GuestCheckoutAllowed);

        var @event = new SettingsChangedIntegrationEvent
        {
            ServiceName = "SaleOrderService",
            Category = "CartCheckout",
            Scope = "System",
            TenantId = null
        };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);

        return new(settings.GuestCheckoutAllowed);
    }

    public async Task<TenantCartCheckoutSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        return await _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantCartCheckoutSettingsDto(settings.TenantId, settings.CartHoldMinutes, settings.RequireRegistrationAtCheckout, settings.CheckoutSteps);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantCartCheckoutSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCartCheckoutSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.CartHoldMinutes, request.RequireRegistrationAtCheckout, request.CheckoutSteps);

        var @event = new SettingsChangedIntegrationEvent
        {
            ServiceName = "SaleOrderService",
            Category = "CartCheckout",
            Scope = "Tenant",
            TenantId = tenantId
        };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);

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
    private const string SystemCacheKey = "settings:saleorder:order:system";
    private static string TenantCacheKey(Guid tenantId) => $"settings:saleorder:order:tenant:{tenantId}";

    private readonly SalesOrderDbContext _dbContext;
    private readonly ICacheService _cache;

    public OrderSettingsService(SalesOrderDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<SystemOrderSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return await _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemOrderSettingsDto(settings.CustomOrderNumberFormatAllowed);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemOrderSettingsDto> UpdateSystemSettingsAsync(UpdateSystemOrderSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.CustomOrderNumberFormatAllowed);

        var @event = new SettingsChangedIntegrationEvent
        {
            ServiceName = "SaleOrderService",
            Category = "Order",
            Scope = "System",
            TenantId = null
        };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);

        return new(settings.CustomOrderNumberFormatAllowed);
    }

    public async Task<TenantOrderSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        return await _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new TenantOrderSettingsDto(settings.TenantId, settings.OrderNumberPrefix, settings.AutoCancelUnpaidAfterHours, settings.AllowEditAfterSubmit);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantOrderSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantOrderSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        settings.Update(request.OrderNumberPrefix, request.AutoCancelUnpaidAfterHours, request.AllowEditAfterSubmit);

        var @event = new SettingsChangedIntegrationEvent
        {
            ServiceName = "SaleOrderService",
            Category = "Order",
            Scope = "Tenant",
            TenantId = tenantId
        };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);

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
