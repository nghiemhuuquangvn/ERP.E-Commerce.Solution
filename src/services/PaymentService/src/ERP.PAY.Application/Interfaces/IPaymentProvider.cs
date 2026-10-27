namespace ERP.PAY.Application.Interfaces;

public record GatewayCreateResult(bool Success, string PaymentUrl, string TransactionRef, string? Error);

/// <summary>Amount is what the gateway says was paid (VND, whole units); the callback handler refuses it when it differs from the payment.</summary>
public record GatewayVerifyResult(bool Success, bool IsPaid, string TransactionRef, string? GatewayTransactionId, string? Error, decimal? Amount = null);

public record GatewayRefundResult(bool Success, string? GatewayRefundId, string? Error, bool Pending = false);

/// <summary>HTTP status and body of the answer to a server-to-server notification, in the shape each gateway expects (MoMo: 204 on success; VNPay and ZaloPay: 200 with their own codes).</summary>
public record GatewayIpnReply(int StatusCode, object? Body);

/// <summary>
/// One tenant's merchant account at a gateway, decrypted for the length of one call. PublicId = TmnCode / partnerCode / app_id;
/// Secret1 / Secret2 as described on TenantGatewayCredential.
/// </summary>
public record GatewayCredentials(string Provider, string PublicId, string? AccessKey, string? Secret1, string? Secret2, bool Sandbox)
{
    public override string ToString() => $"GatewayCredentials({Provider}, {PublicId}, sandbox={Sandbox})";   // never print secrets
}

public record GatewayPaymentRequest(
    Guid PaymentId, decimal Amount, string Currency, string TransactionRef, string OrderInfo,
    string ReturnUrl, string NotifyUrl, DateTime NowUtc);

/// <summary>What the gateway sent: GET query and / or POST body, exactly as received (signatures cover the raw text).</summary>
public record GatewayCallback(string Method, IReadOnlyDictionary<string, string> Query, string Body);

/// <summary>One payment gateway (VNPay, MoMo, ZaloPay). Stateless: the tenant's credentials are passed in on every call.</summary>
public interface IPaymentProvider
{
    /// <summary>"VNPay" | "MoMo" | "ZaloPay" — matches PaymentProviders.</summary>
    string Code { get; }

    /// <summary>A reference that is unique per attempt (a retry of an unpaid payment must not reuse the previous one).</summary>
    string NewTransactionRef(Guid paymentId, DateTime nowUtc);

    Task<GatewayCreateResult> CreatePaymentAsync(GatewayCredentials credentials, GatewayPaymentRequest request, CancellationToken ct = default);

    /// <summary>The payment reference named in a notification, read WITHOUT trusting it — only to find which tenant's keys to verify with. Null when absent.</summary>
    string? PeekTransactionRef(GatewayCallback callback);

    /// <summary>Checks the signature (constant-time) with the tenant's secrets and reads the outcome. Never throws on bad input.</summary>
    GatewayVerifyResult Verify(GatewayCredentials credentials, GatewayCallback callback);

    /// <summary>The answer the gateway expects to its server-to-server notification.</summary>
    GatewayIpnReply IpnResponse(bool accepted, string message);

    Task<GatewayRefundResult> RefundAsync(GatewayCredentials credentials, string gatewayTransactionId, string transactionRef,
        decimal amount, string reason, CancellationToken ct = default);
}

public record ResolvedProvider(IPaymentProvider Provider, GatewayCredentials Credentials);

public record PaymentMethodOption(string Code, string Name, bool IsGateway);

public interface IPaymentProviderResolver
{
    /// <summary>The provider with the CURRENT tenant's credentials. Throws a business-rule exception (HTTP 400) when it is switched off or has no keys.</summary>
    Task<ResolvedProvider> ResolveAsync(string provider, CancellationToken ct = default);

    /// <summary>What this tenant can offer at checkout: COD (if enabled) and each gateway that is available in the system, enabled by the tenant and has keys.</summary>
    Task<IReadOnlyList<PaymentMethodOption>> ListMethodsAsync(CancellationToken ct = default);

    /// <summary>The reference a notification names (unverified), or null. Needs no credentials.</summary>
    string? Peek(string provider, GatewayCallback callback);

    /// <summary>The answer a gateway expects to its notification, in its own format. Needs no credentials.</summary>
    GatewayIpnReply ReplyFor(string provider, bool accepted, string message);

    /// <summary>Stub for payments that never went through a gateway (manual capture): refunds succeed without a call, as before P1-10. Null when the payment names a real gateway.</summary>
    IPaymentProvider? StubFor(string? gatewayProvider);
}
