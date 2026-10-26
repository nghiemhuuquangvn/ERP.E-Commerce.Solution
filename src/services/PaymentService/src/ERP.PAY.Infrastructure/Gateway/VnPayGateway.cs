using ERP.PAY.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ERP.PAY.Infrastructure.Gateway;

/// <summary>
/// VNPay. Docs: https://sandbox.vnpayment.vn/apis/ — PublicId = TmnCode, Secret1 = hash secret.
/// Hosts come from configuration (VnPay:BaseUrl / VnPay:ApiUrl for the sandbox, VnPay:ProductionBaseUrl / VnPay:ProductionApiUrl live), never from the tenant.
/// </summary>
public class VnPayGateway : IPaymentProvider
{
    private const string SandboxPayUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    private const string SandboxApiUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

    private readonly IConfiguration _config;
    private readonly ILogger<VnPayGateway> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public VnPayGateway(IConfiguration config, ILogger<VnPayGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string Code => "VNPay";

    // VNPay wants a reference that is unique per request: payment id prefix + the time of the attempt.
    public string NewTransactionRef(Guid paymentId, DateTime nowUtc) =>
        paymentId.ToString("N")[..10].ToUpperInvariant() + nowUtc.AddHours(7).ToString("yyMMddHHmmss");

    public Task<GatewayCreateResult> CreatePaymentAsync(GatewayCredentials credentials, GatewayPaymentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(credentials.PublicId) || string.IsNullOrEmpty(credentials.Secret1))
            return Task.FromResult(new GatewayCreateResult(false, string.Empty, string.Empty, "VNPay not configured"));

        // VNPay only ever settles Vietnamese Dong: say so rather than send a VND amount under another currency label.
        if (!GatewaySigning.IsVnd(request.Currency))
            return Task.FromResult(new GatewayCreateResult(false, string.Empty, string.Empty, $"VNPay does not support currency '{request.Currency}' — VND only"));

        var baseUrl = credentials.Sandbox
            ? _config["VnPay:BaseUrl"] ?? SandboxPayUrl
            : _config["VnPay:ProductionBaseUrl"] ?? string.Empty;
        if (string.IsNullOrEmpty(baseUrl))
            return Task.FromResult(new GatewayCreateResult(false, string.Empty, string.Empty, "VNPay production URL is not configured"));

        var vn = request.NowUtc.AddHours(7);
        var query = new SortedDictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = credentials.PublicId,
            ["vnp_Amount"] = (GatewaySigning.WholeAmount(request.Amount) * 100).ToString(),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = request.TransactionRef,
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = request.ReturnUrl,
            ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_CreateDate"] = vn.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = vn.AddMinutes(15).ToString("yyyyMMddHHmmss")
        };

        var raw = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var hash = GatewaySigning.HmacSha512Hex(credentials.Secret1, raw);
        _logger.LogInformation("[VNPay] Payment URL created for ref {TxnRef}", request.TransactionRef);
        return Task.FromResult(new GatewayCreateResult(true, $"{baseUrl}?{raw}&vnp_SecureHash={hash}", request.TransactionRef, null));
    }

    public string? PeekTransactionRef(GatewayCallback callback) =>
        callback.Query.TryGetValue("vnp_TxnRef", out var r) && !string.IsNullOrWhiteSpace(r) ? r : null;

    public GatewayVerifyResult Verify(GatewayCredentials credentials, GatewayCallback callback)
    {
        var query = callback.Query;
        if (!query.TryGetValue("vnp_SecureHash", out var received))
            return new GatewayVerifyResult(false, false, string.Empty, null, "Missing secure hash");
        if (string.IsNullOrEmpty(credentials.Secret1))
            return new GatewayVerifyResult(false, false, string.Empty, null, "VNPay not configured");

        var data = new SortedDictionary<string, string>(
            query.Where(kv => kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                 .ToDictionary(kv => kv.Key, kv => kv.Value));
        var raw = string.Join("&", data.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

        if (!GatewaySigning.HexEquals(GatewaySigning.HmacSha512Hex(credentials.Secret1, raw), received))
        {
            _logger.LogWarning("[VNPay] Hash mismatch — possible tampering.");
            return new GatewayVerifyResult(false, false, string.Empty, null, "Hash mismatch");
        }

        var code = query.GetValueOrDefault("vnp_ResponseCode", "");
        var paid = code == "00";
        decimal? amount = long.TryParse(query.GetValueOrDefault("vnp_Amount", ""), out var cents) ? cents / 100m : null;
        return new GatewayVerifyResult(true, paid, query.GetValueOrDefault("vnp_TxnRef", ""), query.GetValueOrDefault("vnp_TransactionNo", ""),
            paid ? null : $"VNPay error: {code}", amount);
    }

    public GatewayIpnReply IpnResponse(bool accepted, string message) =>
        new(200, new { RspCode = accepted ? "00" : "01", Message = message });

    public async Task<GatewayRefundResult> RefundAsync(GatewayCredentials credentials, string gatewayTransactionId, string transactionRef,
        decimal amount, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(credentials.PublicId) || string.IsNullOrEmpty(credentials.Secret1))
            return new GatewayRefundResult(false, null, "VNPay not configured");

        var apiUrl = credentials.Sandbox
            ? _config["VnPay:ApiUrl"] ?? SandboxApiUrl
            : _config["VnPay:ProductionApiUrl"] ?? string.Empty;
        if (string.IsNullOrEmpty(apiUrl)) return new GatewayRefundResult(false, null, "VNPay production API URL is not configured");

        var requestId = Guid.NewGuid().ToString("N")[..16];
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var vnpAmount = GatewaySigning.WholeAmount(amount) * 100;

        // The refund API signs a fixed pipe-delimited field order (not the sorted query string of the payment URL).
        var signData = string.Join("|", requestId, "2.1.0", "refund", "02", transactionRef, vnpAmount.ToString(),
            gatewayTransactionId, createDate, "PaymentService", createDate, "127.0.0.1", reason);

        var payload = new
        {
            vnp_RequestId = requestId, vnp_Version = "2.1.0", vnp_Command = "refund", vnp_TmnCode = credentials.PublicId,
            vnp_TransactionType = "02", vnp_TxnRef = transactionRef, vnp_Amount = vnpAmount, vnp_TransactionNo = gatewayTransactionId,
            vnp_CreateBy = "PaymentService", vnp_OrderInfo = reason, vnp_TransactionDate = createDate, vnp_CreateDate = createDate,
            vnp_IpAddr = "127.0.0.1", vnp_SecureHash = GatewaySigning.HmacSha512Hex(credentials.Secret1, signData)
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var response = await client.PostAsJsonAsync(apiUrl, payload, ct);
            var body = await response.Content.ReadFromJsonAsync<VnPayRefundResponse>(cancellationToken: ct);

            if (body?.vnp_ResponseCode == "00")
                return new GatewayRefundResult(true, body.vnp_TransactionNo ?? requestId, null);

            var error = $"VNPay refund error: {body?.vnp_ResponseCode ?? response.StatusCode.ToString()}";
            _logger.LogWarning("[VNPay] Refund failed for TxnRef={TxnRef}: {Error}", transactionRef, error);
            return new GatewayRefundResult(false, null, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VNPay] Refund request threw for TxnRef={TxnRef}", transactionRef);
            return new GatewayRefundResult(false, null, ex.Message);
        }
    }

    private record VnPayRefundResponse(string? vnp_ResponseCode, string? vnp_TransactionNo, string? vnp_Message);
}

/// <summary>
/// Stand-in for payments that never went through a gateway (captured by hand, or a refund of a payment with no provider):
/// a refund "succeeds" without any call, exactly as the old NullPaymentGateway did. It cannot create or verify anything.
/// </summary>
public class NullPaymentProvider : IPaymentProvider
{
    private readonly ILogger<NullPaymentProvider> _logger;
    public NullPaymentProvider(ILogger<NullPaymentProvider> logger) => _logger = logger;

    public string Code => "Null";
    public string NewTransactionRef(Guid paymentId, DateTime nowUtc) => paymentId.ToString("N");

    public Task<GatewayCreateResult> CreatePaymentAsync(GatewayCredentials credentials, GatewayPaymentRequest request, CancellationToken ct = default) =>
        Task.FromResult(new GatewayCreateResult(false, string.Empty, string.Empty, "No payment gateway"));

    public string? PeekTransactionRef(GatewayCallback callback) => null;

    public GatewayVerifyResult Verify(GatewayCredentials credentials, GatewayCallback callback) =>
        new(false, false, string.Empty, null, "No payment gateway");

    public GatewayIpnReply IpnResponse(bool accepted, string message) => new(400, new { message });

    public Task<GatewayRefundResult> RefundAsync(GatewayCredentials credentials, string gatewayTransactionId, string transactionRef,
        decimal amount, string reason, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV] No gateway: refund for {TxnRef} amount={Amount} recorded without a call", transactionRef, amount);
        return Task.FromResult(new GatewayRefundResult(true, "DEV-REFUND-" + Guid.NewGuid().ToString("N")[..8], null));
    }
}
