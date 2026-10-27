using ERP.PAY.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ERP.PAY.Infrastructure.Gateway;

/// <summary>
/// ZaloPay (API v2). Docs: https://docs.zalopay.vn/ — PublicId = app_id, Secret1 = key1 (signs what we send),
/// Secret2 = key2 (verifies what ZaloPay sends). MACs are HMAC-SHA256 hex. VND only.
/// Hosts: ZaloPay:BaseUrl (default the public sandbox) / ZaloPay:ProductionBaseUrl — never from the tenant.
/// </summary>
public class ZaloPayGateway : IPaymentProvider
{
    private const string SandboxHost = "https://sb-openapi.zalopay.vn";

    private readonly IConfiguration _config;
    private readonly ILogger<ZaloPayGateway> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ZaloPayGateway(IConfiguration config, ILogger<ZaloPayGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string Code => "ZaloPay";

    // app_trans_id must start with yyMMdd of the day in Vietnam (UTC+7) and be unique: yyMMdd_<payment id prefix><time>.
    public string NewTransactionRef(Guid paymentId, DateTime nowUtc)
    {
        var vn = nowUtc.AddHours(7);
        return $"{vn:yyMMdd}_{paymentId.ToString("N")[..10]}{vn:HHmmssfff}";
    }

    private string Host(GatewayCredentials c) =>
        (c.Sandbox ? _config["ZaloPay:BaseUrl"] ?? SandboxHost : _config["ZaloPay:ProductionBaseUrl"] ?? string.Empty).TrimEnd('/');

    public async Task<GatewayCreateResult> CreatePaymentAsync(GatewayCredentials c, GatewayPaymentRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(c.PublicId) || string.IsNullOrEmpty(c.Secret1))
            return new GatewayCreateResult(false, string.Empty, string.Empty, "ZaloPay not configured");
        if (!GatewaySigning.IsVnd(r.Currency))
            return new GatewayCreateResult(false, string.Empty, string.Empty, $"ZaloPay does not support currency '{r.Currency}' — VND only");
        var host = Host(c);
        if (string.IsNullOrEmpty(host)) return new GatewayCreateResult(false, string.Empty, string.Empty, "ZaloPay production URL is not configured");

        var appUser = "erp-" + r.PaymentId.ToString("N")[..8];
        var appTime = new DateTimeOffset(r.NowUtc, TimeSpan.Zero).ToUnixTimeMilliseconds().ToString();
        var amount = GatewaySigning.WholeAmount(r.Amount).ToString();
        var embedData = JsonSerializer.Serialize(new { redirecturl = r.ReturnUrl });
        const string item = "[]";

        var mac = GatewaySigning.HmacSha256Hex(c.Secret1,
            $"{c.PublicId}|{r.TransactionRef}|{appUser}|{amount}|{appTime}|{embedData}|{item}");

        var form = new Dictionary<string, string>
        {
            ["app_id"] = c.PublicId, ["app_user"] = appUser, ["app_time"] = appTime, ["amount"] = amount,
            ["app_trans_id"] = r.TransactionRef, ["embed_data"] = embedData, ["item"] = item, ["description"] = r.OrderInfo,
            ["bank_code"] = "", ["callback_url"] = r.NotifyUrl, ["mac"] = mac
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var response = await client.PostAsync($"{host}/v2/create", new FormUrlEncodedContent(form), ct);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var code = Read(doc.RootElement, "return_code");
            var url = Read(doc.RootElement, "order_url");

            if (code == "1" && !string.IsNullOrEmpty(url))
            {
                _logger.LogInformation("[ZaloPay] Order created for ref {Ref}", r.TransactionRef);
                return new GatewayCreateResult(true, url, r.TransactionRef, null);
            }
            var error = $"ZaloPay error {code}: {Read(doc.RootElement, "return_message")}";
            _logger.LogWarning("[ZaloPay] Create failed for {Ref}: {Error}", r.TransactionRef, error);
            return new GatewayCreateResult(false, string.Empty, string.Empty, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "[ZaloPay] Create request failed for {Ref}", r.TransactionRef);
            return new GatewayCreateResult(false, string.Empty, string.Empty, "ZaloPay is not reachable");
        }
    }

    public string? PeekTransactionRef(GatewayCallback cb)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(cb.Body))
            {
                using var envelope = JsonDocument.Parse(cb.Body);
                var data = Read(envelope.RootElement, "data");
                if (string.IsNullOrEmpty(data)) return null;
                using var inner = JsonDocument.Parse(data);
                return Read(inner.RootElement, "app_trans_id") is { Length: > 0 } id ? id : null;
            }
        }
        catch (JsonException) { return null; }
        return cb.Query.TryGetValue("apptransid", out var q) && !string.IsNullOrWhiteSpace(q) ? q : null;
    }

    // Two shapes: the server callback (POST JSON {data, mac}) — ZaloPay only calls it for a PAID order — and the browser return
    // (GET apptransid / status / checksum …), where status 1 means paid.
    public GatewayVerifyResult Verify(GatewayCredentials c, GatewayCallback cb)
    {
        if (string.IsNullOrEmpty(c.Secret2))
            return new GatewayVerifyResult(false, false, string.Empty, null, "ZaloPay not configured");

        if (!string.IsNullOrWhiteSpace(cb.Body))
            return VerifyCallbackBody(c, cb.Body);
        if (cb.Query.ContainsKey("checksum"))
            return VerifyReturnQuery(c, cb.Query);
        return new GatewayVerifyResult(false, false, string.Empty, null, "Nothing to verify");
    }

    private GatewayVerifyResult VerifyCallbackBody(GatewayCredentials c, string body)
    {
        try
        {
            using var envelope = JsonDocument.Parse(body);
            var data = Read(envelope.RootElement, "data");
            if (!GatewaySigning.HexEquals(GatewaySigning.HmacSha256Hex(c.Secret2!, data), Read(envelope.RootElement, "mac")))
            {
                _logger.LogWarning("[ZaloPay] Callback MAC mismatch — possible tampering.");
                return new GatewayVerifyResult(false, false, string.Empty, null, "MAC mismatch");
            }

            using var inner = JsonDocument.Parse(data);
            if (Read(inner.RootElement, "app_id") != c.PublicId)
                return new GatewayVerifyResult(false, false, string.Empty, null, "app_id mismatch");

            decimal? amount = decimal.TryParse(Read(inner.RootElement, "amount"), out var a) ? a : null;
            return new GatewayVerifyResult(true, true, Read(inner.RootElement, "app_trans_id"), Read(inner.RootElement, "zp_trans_id"), null, amount);
        }
        catch (JsonException)
        {
            return new GatewayVerifyResult(false, false, string.Empty, null, "Malformed body");
        }
    }

    private GatewayVerifyResult VerifyReturnQuery(GatewayCredentials c, IReadOnlyDictionary<string, string> q)
    {
        string G(string k) => q.GetValueOrDefault(k, "");
        var expected = GatewaySigning.HmacSha256Hex(c.Secret2!,
            $"{G("appid")}|{G("apptransid")}|{G("pmcid")}|{G("bankcode")}|{G("amount")}|{G("discountamount")}|{G("status")}");
        if (!GatewaySigning.HexEquals(expected, G("checksum")))
            return new GatewayVerifyResult(false, false, string.Empty, null, "Checksum mismatch");
        if (G("appid") != c.PublicId)
            return new GatewayVerifyResult(false, false, string.Empty, null, "app_id mismatch");

        var paid = G("status") == "1";
        decimal? amount = decimal.TryParse(G("amount"), out var a) ? a : null;
        // The browser return does not carry zp_trans_id; a later server callback fills it in (that one is not skipped: the payment is still Pending until a callback or this return captures it).
        return new GatewayVerifyResult(true, paid, G("apptransid"), null, paid ? null : $"ZaloPay status {G("status")}", amount);
    }

    // return_code 1 = success; -1 asks ZaloPay to call again.
    public GatewayIpnReply IpnResponse(bool accepted, string message) =>
        new(200, new Dictionary<string, object> { ["return_code"] = accepted ? 1 : -1, ["return_message"] = message });

    public async Task<GatewayRefundResult> RefundAsync(GatewayCredentials c, string gatewayTransactionId, string transactionRef,
        decimal amount, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(c.PublicId) || string.IsNullOrEmpty(c.Secret1))
            return new GatewayRefundResult(false, null, "ZaloPay not configured");
        if (string.IsNullOrEmpty(gatewayTransactionId))
            return new GatewayRefundResult(false, null, "The payment has no ZaloPay transaction id (zp_trans_id) to refund");
        var host = Host(c);
        if (string.IsNullOrEmpty(host)) return new GatewayRefundResult(false, null, "ZaloPay production URL is not configured");

        var now = DateTime.UtcNow;
        var timestamp = new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeMilliseconds().ToString();
        var refundId = $"{now.AddHours(7):yyMMdd}_{c.PublicId}_{Guid.NewGuid().ToString("N")[..12]}";
        var amountText = GatewaySigning.WholeAmount(amount).ToString();
        var description = reason ?? string.Empty;

        var mac = GatewaySigning.HmacSha256Hex(c.Secret1, $"{c.PublicId}|{gatewayTransactionId}|{amountText}|{description}|{timestamp}");
        var form = new Dictionary<string, string>
        {
            ["app_id"] = c.PublicId, ["m_refund_id"] = refundId, ["zp_trans_id"] = gatewayTransactionId, ["amount"] = amountText,
            ["timestamp"] = timestamp, ["description"] = description, ["mac"] = mac
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var response = await client.PostAsync($"{host}/v2/refund", new FormUrlEncodedContent(form), ct);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var code = Read(doc.RootElement, "return_code");
            var refundRef = Read(doc.RootElement, "refund_id") is { Length: > 0 } id ? id : refundId;

            if (code == "1") return new GatewayRefundResult(true, refundRef, null);
            // 3 = ZaloPay accepted the request and is still processing it: not a failure, not yet confirmed money (DEFERRED: no status poll yet).
            if (code == "3") return new GatewayRefundResult(false, refundRef, "ZaloPay is still processing the refund", Pending: true);

            var error = $"ZaloPay refund error {code}: {Read(doc.RootElement, "return_message")}";
            _logger.LogWarning("[ZaloPay] Refund failed for {Ref}: {Error}", transactionRef, error);
            return new GatewayRefundResult(false, null, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "[ZaloPay] Refund request failed for {Ref}", transactionRef);
            return new GatewayRefundResult(false, null, "ZaloPay is not reachable");
        }
    }

    private static string Read(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v)) return string.Empty;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            _ => string.Empty
        };
    }
}
