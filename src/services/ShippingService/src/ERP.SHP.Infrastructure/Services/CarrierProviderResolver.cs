using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using ERP.SHP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>
/// Picks a carrier integration and the CURRENT tenant's account for it (the tenant comes from the request or from the TenantScope of the caller).
/// A carrier is usable when the integration is real, the platform offers it (SystemShippingSettings), the shop switched it on and its account is
/// complete. Mirrors PaymentProviderResolver.
/// </summary>
public class CarrierProviderResolver : ICarrierProviderResolver
{
    private readonly ShippingDbContext _db;
    private readonly ISecretProtector _secrets;
    private readonly IReadOnlyDictionary<string, ICarrierProvider> _providers;

    public CarrierProviderResolver(ShippingDbContext db, ISecretProtector secrets, IEnumerable<ICarrierProvider> providers)
    {
        _db = db;
        _secrets = secrets;
        _providers = providers.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
    }

    public ICarrierProvider? Provider(string carrierCode) =>
        CarrierCodes.Normalize(carrierCode) is { } code && _providers.TryGetValue(code, out var p) ? p : null;

    public async Task<ResolvedCarrier> ResolveAsync(string carrierCode, CancellationToken ct = default)
    {
        var code = CarrierCodes.Normalize(carrierCode);
        if (code == null || !_providers.TryGetValue(code, out var provider))
            throw new BusinessRuleException("Carrier", $"'{carrierCode}' is not a carrier this platform integrates.");
        if (!provider.Implemented)
            throw new BusinessRuleException("Carrier", $"{code} is not integrated yet.");

        var system = await _db.SystemShippingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (!(system?.IsAvailable(code) ?? true))
            throw new BusinessRuleException("Carrier", $"{code} is not available on this platform.");

        var row = await _db.TenantCarrierCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.CarrierCode == code, ct);
        var credentials = row is { Enabled: true } ? ToCredentials(row) : null;
        if (credentials == null || !IsComplete(credentials))
            throw new BusinessRuleException("Carrier", $"{code} is not enabled for this shop or its account is incomplete.");
        return new ResolvedCarrier(provider, credentials);
    }

    public async Task<IReadOnlyList<ResolvedCarrier>> ListEnabledAsync(CancellationToken ct = default)
    {
        var list = new List<ResolvedCarrier>();
        foreach (var code in CarrierCodes.All)
        {
            try { list.Add(await ResolveAsync(code, ct)); }
            catch (BusinessRuleException) { /* not usable: off, unavailable, no account, or a placeholder */ }
        }
        return list;
    }

    private CarrierCredentials ToCredentials(TenantCarrierCredential row) => new(
        row.CarrierCode, row.PublicId, row.Username,
        string.IsNullOrEmpty(row.Secret1Encrypted) ? null : _secrets.Unprotect(row.Secret1Encrypted),
        string.IsNullOrEmpty(row.Secret2Encrypted) ? null : _secrets.Unprotect(row.Secret2Encrypted),
        row.Sandbox);

    /// <summary>What each carrier needs before it can quote, book or verify a webhook.</summary>
    public static bool IsComplete(CarrierCredentials c) => c.CarrierCode switch
    {
        CarrierCodes.Ghtk or CarrierCodes.Ghn => !string.IsNullOrEmpty(c.PublicId) && !string.IsNullOrEmpty(c.Secret1),
        CarrierCodes.ViettelPost => !string.IsNullOrEmpty(c.Username) && !string.IsNullOrEmpty(c.Secret1),
        _ => false
    };
}
