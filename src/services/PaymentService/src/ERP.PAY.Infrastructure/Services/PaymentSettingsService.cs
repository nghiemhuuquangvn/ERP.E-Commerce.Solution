using ERP.PAY.Application.DTOs.Settings;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities.Settings;
using ERP.PAY.Infrastructure.Persistence;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;

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
    private readonly PaymentDbContext _dbContext;
    private readonly ISecretProtector _secretProtector;

    public PaymentSettingsService(PaymentDbContext dbContext, ISecretProtector secretProtector)
    {
        _dbContext = dbContext;
        _secretProtector = secretProtector;
    }

    public async Task<SystemPaymentSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.VnPayAvailable, settings.ManualBankTransferAvailable);
    }

    public async Task<SystemPaymentSettingsDto> UpdateSystemSettingsAsync(UpdateSystemPaymentSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateSystemAsync(ct);
        settings.Update(request.VnPayAvailable, request.ManualBankTransferAvailable);
        await _dbContext.SaveChangesAsync(ct);
        return new(settings.VnPayAvailable, settings.ManualBankTransferAvailable);
    }

    public async Task<TenantPaymentSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<TenantPaymentSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantPaymentSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateTenantAsync(tenantId, ct);
        var encryptedKey = string.IsNullOrEmpty(request.VnPaySecretKey) ? null : _secretProtector.Protect(request.VnPaySecretKey);
        settings.Update(request.VnPayEnabled, encryptedKey, request.SandboxMode);
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(settings);
    }

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
        new(s.TenantId, s.VnPayEnabled, !string.IsNullOrEmpty(s.VnPaySecretKeyEncrypted), s.SandboxMode);
}
