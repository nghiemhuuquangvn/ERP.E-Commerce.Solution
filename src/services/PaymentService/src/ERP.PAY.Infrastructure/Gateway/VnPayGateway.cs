using ERP.PAY.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace ERP.PAY.Infrastructure.Gateway;

/// <summary>
/// VNPay payment gateway integration.
/// Docs: https://sandbox.vnpayment.vn/apis/
/// Required config: VnPay:TmnCode, VnPay:HashSecret, VnPay:BaseUrl, VnPay:ReturnUrl
/// </summary>
public class VnPayGateway : IPaymentGateway
{
    private readonly IConfiguration _config;
    private readonly ILogger<VnPayGateway> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public VnPayGateway(IConfiguration config, ILogger<VnPayGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public Task<GatewayCreateResult> CreatePaymentUrlAsync(
        Guid paymentId, decimal amount, string currency, string returnUrl, CancellationToken ct = default)
    {
        var tmnCode = _config["VnPay:TmnCode"] ?? string.Empty;
        var hashSecret = _config["VnPay:HashSecret"] ?? string.Empty;
        var baseUrl = _config["VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

        if (string.IsNullOrEmpty(tmnCode) || string.IsNullOrEmpty(hashSecret))
        {
            _logger.LogWarning("[VNPay] Credentials not configured.");
            return Task.FromResult(new GatewayCreateResult(false, string.Empty, string.Empty, "VNPay not configured"));
        }

        // VNPay only ever settles in Vietnamese Dong — the domain model (Payment.Currency)
        // supports arbitrary currencies for record-keeping, but this specific gateway
        // cannot process anything else, so say so explicitly rather than silently
        // sending a VND amount under a different currency label.
        if (!string.Equals(currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[VNPay] Unsupported currency {Currency} — VNPay only settles VND.", currency);
            return Task.FromResult(new GatewayCreateResult(false, string.Empty, string.Empty, $"VNPay does not support currency '{currency}' — VND only"));
        }

        var vnpAmountInt = (long)(amount * 100);
        var txnRef = paymentId.ToString("N")[..16].ToUpper();
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");

        var queryParams = new SortedDictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = vnpAmountInt.ToString(),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = txnRef,
            ["vnp_OrderInfo"] = $"Payment for order {paymentId}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_CreateDate"] = createDate,
            ["vnp_ExpireDate"] = DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss")
        };

        var rawData = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var secureHash = ComputeHmacSha512(hashSecret, rawData);
        var paymentUrl = $"{baseUrl}?{rawData}&vnp_SecureHash={secureHash}";

        _logger.LogInformation("[VNPay] Payment URL created for ref {TxnRef}", txnRef);
        return Task.FromResult(new GatewayCreateResult(true, paymentUrl, txnRef, null));
    }

    public Task<GatewayVerifyResult> VerifyCallbackAsync(IReadOnlyDictionary<string, string> queryParams)
    {
        var hashSecret = _config["VnPay:HashSecret"] ?? string.Empty;

        if (!queryParams.TryGetValue("vnp_SecureHash", out var receivedHash))
            return Task.FromResult(new GatewayVerifyResult(false, false, string.Empty, null, "Missing secure hash"));

        var dataParams = new SortedDictionary<string, string>(
            queryParams.Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                       .ToDictionary(kv => kv.Key, kv => kv.Value));

        var rawData = string.Join("&", dataParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var expectedHash = ComputeHmacSha512(hashSecret, rawData);

        if (!string.Equals(expectedHash, receivedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[VNPay] Hash mismatch — possible tampering.");
            return Task.FromResult(new GatewayVerifyResult(false, false, string.Empty, null, "Hash mismatch"));
        }

        var responseCode = queryParams.GetValueOrDefault("vnp_ResponseCode", "");
        var txnRef = queryParams.GetValueOrDefault("vnp_TxnRef", "");
        var gatewayTxnId = queryParams.GetValueOrDefault("vnp_TransactionNo", "");
        var isPaid = responseCode == "00";

        _logger.LogInformation("[VNPay] Callback verified: TxnRef={TxnRef} ResponseCode={Code} Paid={Paid}",
            txnRef, responseCode, isPaid);

        return Task.FromResult(new GatewayVerifyResult(true, isPaid, txnRef, gatewayTxnId, isPaid ? null : $"VNPay error: {responseCode}"));
    }

    public async Task<GatewayRefundResult> RefundAsync(
        string gatewayTransactionId, string transactionRef, decimal amount, string reason, CancellationToken ct = default)
    {
        var tmnCode = _config["VnPay:TmnCode"] ?? string.Empty;
        var hashSecret = _config["VnPay:HashSecret"] ?? string.Empty;
        var apiUrl = _config["VnPay:ApiUrl"] ?? "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

        if (string.IsNullOrEmpty(tmnCode) || string.IsNullOrEmpty(hashSecret))
        {
            _logger.LogWarning("[VNPay] Credentials not configured — cannot process refund.");
            return new GatewayRefundResult(false, null, "VNPay not configured");
        }

        var requestId = Guid.NewGuid().ToString("N")[..16];
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var vnpAmountInt = (long)(amount * 100);

        // VNPay's refund API signs a fixed pipe-delimited field order (not the
        // alphabetically-sorted querystring format used for payment-URL creation).
        var signData = string.Join("|",
            requestId, "2.1.0", "refund", "02", transactionRef, vnpAmountInt.ToString(),
            gatewayTransactionId, createDate, "PaymentService", createDate, "127.0.0.1", reason);
        var secureHash = ComputeHmacSha512(hashSecret, signData);

        var payload = new
        {
            vnp_RequestId = requestId,
            vnp_Version = "2.1.0",
            vnp_Command = "refund",
            vnp_TmnCode = tmnCode,
            vnp_TransactionType = "02", // full or partial refund of a successful transaction
            vnp_TxnRef = transactionRef,
            vnp_Amount = vnpAmountInt,
            vnp_TransactionNo = gatewayTransactionId,
            vnp_CreateBy = "PaymentService",
            vnp_OrderInfo = reason,
            vnp_TransactionDate = createDate,
            vnp_CreateDate = createDate,
            vnp_IpAddr = "127.0.0.1",
            vnp_SecureHash = secureHash
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(apiUrl, payload, ct);
            var body = await response.Content.ReadFromJsonAsync<VnPayRefundResponse>(cancellationToken: ct);

            if (body?.vnp_ResponseCode == "00")
            {
                _logger.LogInformation("[VNPay] Refund succeeded for TxnRef={TxnRef}", transactionRef);
                return new GatewayRefundResult(true, body.vnp_TransactionNo ?? requestId, null);
            }

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

    private static string ComputeHmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}

/// <summary>
/// Null/stub gateway for development — immediately returns success.
/// </summary>
public class NullPaymentGateway : IPaymentGateway
{
    private readonly ILogger<NullPaymentGateway> _logger;
    public NullPaymentGateway(ILogger<NullPaymentGateway> logger) => _logger = logger;

    public Task<GatewayCreateResult> CreatePaymentUrlAsync(
        Guid paymentId, decimal amount, string currency, string returnUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV] NullGateway: CreatePayment for {PaymentId} amount={Amount}", paymentId, amount);
        var stubUrl = $"{returnUrl}?paymentId={paymentId}&status=success&txnRef={paymentId:N}";
        return Task.FromResult(new GatewayCreateResult(true, stubUrl, paymentId.ToString("N"), null));
    }

    public Task<GatewayVerifyResult> VerifyCallbackAsync(IReadOnlyDictionary<string, string> queryParams)
    {
        _logger.LogInformation("[DEV] NullGateway: VerifyCallback");
        var txnRef = queryParams.GetValueOrDefault("txnRef", Guid.NewGuid().ToString("N"));
        return Task.FromResult(new GatewayVerifyResult(true, true, txnRef, "DEV-" + txnRef[..8], null));
    }

    public Task<GatewayRefundResult> RefundAsync(string gatewayTransactionId, string transactionRef, decimal amount, string reason, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV] NullGateway: Refund for {TxnRef} amount={Amount}", transactionRef, amount);
        return Task.FromResult(new GatewayRefundResult(true, "DEV-REFUND-" + Guid.NewGuid().ToString("N")[..8], null));
    }
}
