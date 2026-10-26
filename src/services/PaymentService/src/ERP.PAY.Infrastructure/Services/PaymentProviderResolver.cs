using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Infrastructure.Persistence;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ERP.PAY.Infrastructure.Services;

/// <summary>
/// Picks a gateway and the CURRENT tenant's merchant account for it (the tenant comes from the request or the TenantScope of the caller).
/// A gateway is usable when the platform offers it (SystemPaymentSettings), the tenant switched it on and the keys are there.
/// VNPay keeps its two older sources so nothing that worked before breaks: the tenant's single secret in TenantPaymentSettings
/// (with the TmnCode from configuration) and the global VnPay:* configuration.
/// </summary>
public class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly PaymentDbContext _db;
    private readonly ISecretProtector _secrets;
    private readonly IConfiguration _config;
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;
    private readonly IPaymentProvider _stub;

    public PaymentProviderResolver(PaymentDbContext db, ISecretProtector secrets, IConfiguration config,
        IEnumerable<IPaymentProvider> providers)
    {
        _db = db;
        _secrets = secrets;
        _config = config;
        var all = providers.ToList();
        _stub = all.First(p => p.Code == "Null");
        _providers = all.Where(p => p.Code != "Null").ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ResolvedProvider> ResolveAsync(string provider, CancellationToken ct = default)
    {
        var code = PaymentProviders.Normalize(provider);
        if (code == null || !_providers.TryGetValue(code, out var gateway))
            throw new BusinessRuleException("Payment", $"'{provider}' is not an online payment method.");

        var system = await _db.SystemPaymentSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var available = code switch
        {
            PaymentProviders.VnPay => system?.VnPayAvailable ?? true,
            PaymentProviders.MoMo => system?.MoMoAvailable ?? true,
            PaymentProviders.ZaloPay => system?.ZaloPayAvailable ?? true,
            _ => false
        };
        if (!available)
            throw new BusinessRuleException("Payment", $"{code} is not available on this platform.");

        var credentials = await LoadCredentialsAsync(code, ct)
            ?? throw new BusinessRuleException("Payment", $"{code} is not enabled for this shop or has no keys yet.");
        return new ResolvedProvider(gateway, credentials);
    }

    public async Task<IReadOnlyList<PaymentMethodOption>> ListMethodsAsync(CancellationToken ct = default)
    {
        var options = new List<PaymentMethodOption>();
        var settings = await _db.TenantPaymentSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings?.CodEnabled ?? true)
            options.Add(new PaymentMethodOption(PaymentProviders.Cod, "Cash on delivery", false));

        foreach (var gateway in PaymentProviders.Gateways)
        {
            try
            {
                await ResolveAsync(gateway, ct);
                options.Add(new PaymentMethodOption(gateway, gateway, true));
            }
            catch (BusinessRuleException)
            {
                // Not offered: switched off, not available, or no keys.
            }
        }
        return options;
    }

    public string? Peek(string provider, GatewayCallback callback) =>
        PaymentProviders.Normalize(provider) is { } code && _providers.TryGetValue(code, out var p) ? p.PeekTransactionRef(callback) : null;

    public GatewayIpnReply ReplyFor(string provider, bool accepted, string message) =>
        PaymentProviders.Normalize(provider) is { } code && _providers.TryGetValue(code, out var p)
            ? p.IpnResponse(accepted, message)
            : new GatewayIpnReply(400, new { message });

    public IPaymentProvider? StubFor(string? gatewayProvider) =>
        string.IsNullOrEmpty(gatewayProvider) || gatewayProvider is "Null" or PaymentProviders.Cod ? _stub : null;

    private async Task<GatewayCredentials?> LoadCredentialsAsync(string code, CancellationToken ct)
    {
        var row = await _db.TenantGatewayCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == code, ct);
        if (row is { Enabled: true } && !string.IsNullOrEmpty(row.PublicId) && !string.IsNullOrEmpty(row.Secret1Encrypted))
        {
            var credentials = new GatewayCredentials(code, row.PublicId,
                string.IsNullOrEmpty(row.AccessKey) ? null : _secrets.Unprotect(row.AccessKey),
                _secrets.Unprotect(row.Secret1Encrypted),
                string.IsNullOrEmpty(row.Secret2Encrypted) ? null : _secrets.Unprotect(row.Secret2Encrypted), row.Sandbox);
            return IsComplete(credentials) ? credentials : null;
        }

        if (code != PaymentProviders.VnPay) return null;

        // Older VNPay sources.
        var tmn = _config["VnPay:TmnCode"];
        if (string.IsNullOrEmpty(tmn)) return null;
        var tenant = await _db.TenantPaymentSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (tenant is { VnPayEnabled: true } && !string.IsNullOrEmpty(tenant.VnPaySecretKeyEncrypted))
            return new GatewayCredentials(code, tmn, null, _secrets.Unprotect(tenant.VnPaySecretKeyEncrypted), null, tenant.SandboxMode);

        var globalSecret = _config["VnPay:HashSecret"];
        return string.IsNullOrEmpty(globalSecret) ? null : new GatewayCredentials(code, tmn, null, globalSecret, null, true);
    }

    /// <summary>What each gateway needs before it can create a payment or verify a notification.</summary>
    private static bool IsComplete(GatewayCredentials c) => c.Provider switch
    {
        PaymentProviders.MoMo => !string.IsNullOrEmpty(c.AccessKey),
        PaymentProviders.ZaloPay => !string.IsNullOrEmpty(c.Secret2),
        _ => true
    };
}
