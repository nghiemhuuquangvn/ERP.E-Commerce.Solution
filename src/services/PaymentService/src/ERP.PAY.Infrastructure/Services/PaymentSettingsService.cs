using ERP.PAY.Application.DTOs.Settings;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities.Settings;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Infrastructure.Persistence;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.PAY.Infrastructure.Services;

/// <summary>
/// Storage+CRUD only for now — not yet wired into which gateway PaymentCommandHandlers
/// actually use at runtime. Mirrors IdentityService's SettingsService lazy-create-on-read
/// pattern, talking to PaymentDbContext directly rather than through a dedicated
/// repository layer. VnPaySecretKey is encrypted via ISecretProtector before storage and
/// never returned to callers — GetTenantSettingsAsync only reports whether one is set.
/// </summary>
public class PaymentSettingsService : IPaymentSettingsService
{
    private const string SystemCacheKey = "settings:payment:payment:system";

    private readonly PaymentDbContext _dbContext;
    private readonly ISecretProtector _secretProtector;
    private readonly ICacheService _cache;

    public PaymentSettingsService(PaymentDbContext dbContext, ISecretProtector secretProtector, ICacheService cache)
    {
        _dbContext = dbContext;
        _secretProtector = secretProtector;
        _cache = cache;
    }

    public Task<SystemPaymentSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        return _cache.GetOrSetAsync(SystemCacheKey, async () =>
        {
            var settings = await GetOrCreateSystemAsync(ct);
            await _dbContext.SaveChangesAsync(ct);
            return new SystemPaymentSettingsDto(settings.VnPayAvailable, settings.ManualBankTransferAvailable, settings.MoMoAvailable, settings.ZaloPayAvailable);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<SystemPaymentSettingsDto> UpdateSystemSettingsAsync(UpdateSystemPaymentSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.VnPayAvailable, request.ManualBankTransferAvailable, request.MoMoAvailable, request.ZaloPayAvailable);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "PaymentService", Category = "Payment", Scope = "System", TenantId = null };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync(SystemCacheKey, ct);
        return new(settings.VnPayAvailable, settings.ManualBankTransferAvailable, settings.MoMoAvailable, settings.ZaloPayAvailable);
    }

    public Task<TenantPaymentSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"settings:payment:payment:tenant:{tenantId}";
        return _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var settings = await GetOrCreateTenantAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(settings);
        }, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<TenantPaymentSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantPaymentSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        var encryptedKey = string.IsNullOrEmpty(request.VnPaySecretKey) ? null : _secretProtector.Protect(request.VnPaySecretKey);
        settings.Update(request.VnPayEnabled, encryptedKey, request.SandboxMode, request.CodEnabled);

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "PaymentService", Category = "Payment", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"settings:payment:payment:tenant:{tenantId}", ct);
        return ToDto(settings);
    }

    public async Task<IReadOnlyList<GatewayCredentialDto>> GetGatewaysAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _dbContext.TenantGatewayCredentials.AsNoTracking().Where(c => c.TenantId == tenantId).ToListAsync(ct);
        // Every gateway is listed, configured or not, so the admin screen always shows the same three cards.
        return PaymentProviders.Gateways
            .Select(p => rows.FirstOrDefault(r => r.Provider == p) is { } r ? ToDto(r) : new GatewayCredentialDto(p, false, true, null, false, false, false))
            .ToList();
    }

    public async Task<GatewayCredentialDto> UpdateGatewayAsync(Guid tenantId, string provider, UpdateGatewayCredentialRequest request, CancellationToken ct)
    {
        var code = PaymentProviders.Normalize(provider);
        if (code == null || !PaymentProviders.IsGateway(code))
            throw new InvalidOperationException($"'{provider}' is not an online payment gateway.");

        var row = await _dbContext.TenantGatewayCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Provider == code, ct);
        if (row == null)
        {
            row = TenantGatewayCredential.Create(tenantId, code);
            _dbContext.TenantGatewayCredentials.Add(row);
        }

        string? Protect(string? v) => string.IsNullOrWhiteSpace(v) ? null : _secretProtector.Protect(v.Trim());
        row.Update(request.Enabled, request.Sandbox, request.PublicId, Protect(request.AccessKey), Protect(request.Secret1), Protect(request.Secret2));

        var @event = new SettingsChangedIntegrationEvent { ServiceName = "PaymentService", Category = "Payment", Scope = "Tenant", TenantId = tenantId };
        var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        _dbContext.OutboxMessages.Add(new OutboxMessage(nameof(SettingsChangedIntegrationEvent), content));

        await _dbContext.SaveChangesAsync(ct);
        return ToDto(row);
    }

    private static GatewayCredentialDto ToDto(TenantGatewayCredential r) =>
        new(r.Provider, r.Enabled, r.Sandbox, r.PublicId, !string.IsNullOrEmpty(r.AccessKey), !string.IsNullOrEmpty(r.Secret1Encrypted), !string.IsNullOrEmpty(r.Secret2Encrypted));

    private async Task<SystemPaymentSettings> GetOrCreateSystemAsync(CancellationToken ct)
    {
        var settings = await _dbContext.Set<SystemPaymentSettings>().FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = SystemPaymentSettings.CreateDefault();
        _dbContext.Set<SystemPaymentSettings>().Add(settings);
        return settings;
    }

    private async Task<TenantPaymentSettings> GetOrCreateTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _dbContext.Set<TenantPaymentSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings != null) return settings;
        settings = TenantPaymentSettings.CreateDefault(tenantId);
        _dbContext.Set<TenantPaymentSettings>().Add(settings);
        return settings;
    }

    private static TenantPaymentSettingsDto ToDto(TenantPaymentSettings s) =>
        new(s.TenantId, s.VnPayEnabled, !string.IsNullOrEmpty(s.VnPaySecretKeyEncrypted), s.SandboxMode, s.CodEnabled);
}
