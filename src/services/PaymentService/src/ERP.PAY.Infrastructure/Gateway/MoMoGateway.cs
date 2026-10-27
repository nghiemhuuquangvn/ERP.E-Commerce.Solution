using ERP.PAY.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.PAY.Infrastructure.Gateway;

/// <summary>
/// MoMo wallet, "captureWallet" (API v2). Docs: https://developers.momo.vn/v3/ — PublicId = partnerCode, AccessKey, Secret1 = secret key.
/// Signatures are HMAC-SHA256 over a "key=value&amp;…" string with the keys in alphabetical order. VND only.
/// Hosts: MoMo:BaseUrl (default the public test host) for a sandbox account, MoMo:ProductionBaseUrl for a live one — never from the tenant.
/// </summary>
public class MoMoGateway : IPaymentProvider
{
    private const string SandboxHost = "https://test-payment.momo.vn";

    private readonly IConfiguration _config;
    private readonly ILogger<MoMoGateway> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public MoMoGateway(IConfiguration config, ILogger<MoMoGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string Code => "MoMo";

    // MoMo's orderId must be unique per request (max 50 characters); the milliseconds keep two attempts in the same second apart.
    public string NewTransactionRef(Guid paymentId, DateTime nowUtc) =>
        "M" + paymentId.ToString("N")[..12].ToUpperInvariant() + nowUtc.ToString("yyMMddHHmmssfff");

    private string Host(GatewayCredentials c) =>
        (c.Sandbox ? _config["MoMo:BaseUrl"] ?? SandboxHost : _config["MoMo:ProductionBaseUrl"] ?? string.Empty).TrimEnd('/');

    public async Task<GatewayCreateResult> CreatePaymentAsync(GatewayCredentials c, GatewayPaymentRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(c.PublicId) || string.IsNullOrEmpty(c.AccessKey) || string.IsNullOrEmpty(c.Secret1))
            return new GatewayCreateResult(false, string.Empty, string.Empty, "MoMo not configured");
        if (!GatewaySigning.IsVnd(r.Currency))
            return new GatewayCreateResult(false, string.Empty, string.Empty, $"MoMo does not support currency '{r.Currency}' — VND only");
        var host = Host(c);
        if (string.IsNullOrEmpty(host)) return new GatewayCreateResult(false, string.Empty, string.Empty, "MoMo production URL is not configured");

        var amount = GatewaySigning.WholeAmount(r.Amount).ToString();
        var requestId = r.TransactionRef;       // one request per attempt: the reference is already unique
        const string extraData = "", requestType = "captureWallet";

        var raw = $"accessKey={c.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={r.NotifyUrl}&orderId={r.TransactionRef}" +
                  $"&orderInfo={r.OrderInfo}&partnerCode={c.PublicId}&redirectUrl={r.ReturnUrl}&requestId={requestId}&requestType={requestType}";

        var body = new Dictionary<string, object?>
        {
            ["partnerCode"] = c.PublicId, ["requestId"] = requestId, ["amount"] = long.Parse(amount), ["orderId"] = r.TransactionRef,
            ["orderInfo"] = r.OrderInfo, ["redirectUrl"] = r.ReturnUrl, ["ipnUrl"] = r.NotifyUrl, ["requestType"] = requestType,
            ["extraData"] = extraData, ["lang"] = "vi", ["autoCapture"] = true,
            ["signature"] = GatewaySigning.HmacSha256Hex(c.Secret1, raw)
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var response = await client.PostAsJsonAsync($"{host}/v2/gateway/api/create", body, ct);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var resultCode = Read(root, "resultCode");
            var payUrl = Read(root, "payUrl");

            if (resultCode == "0" && !string.IsNullOrEmpty(payUrl))
            {
                _logger.LogInformation("[MoMo] Payment URL created for ref {Ref}", r.TransactionRef);
                return new GatewayCreateResult(true, payUrl, r.TransactionRef, null);
            }
            var error = $"MoMo error {resultCode}: {Read(root, "message")}";
            _logger.LogWarning("[MoMo] Create failed for {Ref}: {Error}", r.TransactionRef, error);
            return new GatewayCreateResult(false, string.Empty, string.Empty, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "[MoMo] Create request failed for {Ref}", r.TransactionRef);
            return new GatewayCreateResult(false, string.Empty, string.Empty, "MoMo is not reachable");
        }
    }

    public string? PeekTransactionRef(GatewayCallback cb)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(cb.Body))
            {
                using var doc = JsonDocument.Parse(cb.Body);
                return Read(doc.RootElement, "orderId") is { Length: > 0 } id ? id : null;
            }
        }
        catch (JsonException) { return null; }
        return cb.Query.TryGetValue("orderId", out var q) && !string.IsNullOrWhiteSpace(q) ? q : null;
    }

    // The notification (POST JSON) and the browser return (GET query) carry the same signed fields.
    public GatewayVerifyResult Verify(GatewayCredentials c, GatewayCallback cb)
    {
        if (string.IsNullOrEmpty(c.AccessKey) || string.IsNullOrEmpty(c.Secret1))
            return new GatewayVerifyResult(false, false, string.Empty, null, "MoMo not configured");

        Func<string, string> get;
        try
        {
            if (!string.IsNullOrWhiteSpace(cb.Body))
            {
                var doc = JsonDocument.Parse(cb.Body);
                get = key => Read(doc.RootElement, key);
            }
            else get = key => cb.Query.GetValueOrDefault(key, "");
        }
        catch (JsonException)
        {
            return new GatewayVerifyResult(false, false, string.Empty, null, "Malformed body");
        }

        var raw = $"accessKey={c.AccessKey}&amount={get("amount")}&extraData={get("extraData")}&message={get("message")}&orderId={get("orderId")}" +
                  $"&orderInfo={get("orderInfo")}&orderType={get("orderType")}&partnerCode={get("partnerCode")}&payType={get("payType")}" +
                  $"&requestId={get("requestId")}&responseTime={get("responseTime")}&resultCode={get("resultCode")}&transId={get("transId")}";

        if (!GatewaySigning.HexEquals(GatewaySigning.HmacSha256Hex(c.Secret1, raw), get("signature")))
        {
            _logger.LogWarning("[MoMo] Signature mismatch — possible tampering.");
            return new GatewayVerifyResult(false, false, string.Empty, null, "Signature mismatch");
        }
        if (!string.Equals(get("partnerCode"), c.PublicId, StringComparison.Ordinal))
            return new GatewayVerifyResult(false, false, string.Empty, null, "Partner code mismatch");

        var paid = get("resultCode") == "0";
        decimal? amount = decimal.TryParse(get("amount"), out var a) ? a : null;
        return new GatewayVerifyResult(true, paid, get("orderId"), get("transId"), paid ? null : $"MoMo error {get("resultCode")}: {get("message")}", amount);
    }

    // MoMo wants HTTP 204 once the notification is accepted; anything else makes it retry.
    public GatewayIpnReply IpnResponse(bool accepted, string message) =>
        accepted ? new GatewayIpnReply(204, null) : new GatewayIpnReply(400, new { message });

    public async Task<GatewayRefundResult> RefundAsync(GatewayCredentials c, string gatewayTransactionId, string transactionRef,
        decimal amount, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(c.PublicId) || string.IsNullOrEmpty(c.AccessKey) || string.IsNullOrEmpty(c.Secret1))
            return new GatewayRefundResult(false, null, "MoMo not configured");
        if (!long.TryParse(gatewayTransactionId, out var transId))
            return new GatewayRefundResult(false, null, "The payment has no MoMo transaction id to refund");
        var host = Host(c);
        if (string.IsNullOrEmpty(host)) return new GatewayRefundResult(false, null, "MoMo production URL is not configured");

        var refundOrderId = "R" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();   // the refund is its own MoMo order
        var requestId = refundOrderId;
        var amountText = GatewaySigning.WholeAmount(amount).ToString();
        var description = reason ?? string.Empty;

        var raw = $"accessKey={c.AccessKey}&amount={amountText}&description={description}&orderId={refundOrderId}&partnerCode={c.PublicId}&requestId={requestId}&transId={transId}";
        var body = new Dictionary<string, object?>
        {
            ["partnerCode"] = c.PublicId, ["orderId"] = refundOrderId, ["requestId"] = requestId, ["amount"] = long.Parse(amountText),
            ["transId"] = transId, ["lang"] = "vi", ["description"] = description,
            ["signature"] = GatewaySigning.HmacSha256Hex(c.Secret1, raw)
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var response = await client.PostAsJsonAsync($"{host}/v2/gateway/api/refund", body, ct);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var code = Read(doc.RootElement, "resultCode");
            if (code == "0")
                return new GatewayRefundResult(true, Read(doc.RootElement, "transId") is { Length: > 0 } id ? id : refundOrderId, null);

            var error = $"MoMo refund error {code}: {Read(doc.RootElement, "message")}";
            _logger.LogWarning("[MoMo] Refund failed for {Ref}: {Error}", transactionRef, error);
            return new GatewayRefundResult(false, null, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "[MoMo] Refund request failed for {Ref}", transactionRef);
            return new GatewayRefundResult(false, null, "MoMo is not reachable");
        }
    }

    /// <summary>A JSON value as the text MoMo signs: strings as they are, numbers as written, anything else empty.</summary>
    private static string Read(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v)) return string.Empty;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }
}
