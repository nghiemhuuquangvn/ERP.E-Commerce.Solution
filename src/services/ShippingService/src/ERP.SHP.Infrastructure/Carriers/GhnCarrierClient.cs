using ERP.SHP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.SHP.Infrastructure.Carriers;

/// <summary>
/// GHN (Giao Hang Nhanh) carrier API client.
/// Docs: https://api.ghn.vn/home/docs/detail
/// Required config: Ghn:Token, Ghn:ShopId, Ghn:BaseUrl
/// </summary>
public class GhnCarrierClient : ICarrierClient
{
    private readonly IConfiguration _config;
    private readonly ILogger<GhnCarrierClient> _logger;
    private readonly HttpClient _http;

    public GhnCarrierClient(IConfiguration config, ILogger<GhnCarrierClient> logger, IHttpClientFactory factory)
    {
        _config = config;
        _logger = logger;
        _http = factory.CreateClient("GHN");
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body = null)
    {
        var baseUrl = _config["Ghn:BaseUrl"] ?? "https://dev-online-gateway.ghn.vn/shiip/public-api";
        var token = _config["Ghn:Token"] ?? string.Empty;
        var shopId = _config["Ghn:ShopId"] ?? string.Empty;

        var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
        request.Headers.Add("Token", token);
        request.Headers.Add("ShopId", shopId);

        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        return request;
    }

    public async Task<CreateOrderResult> CreateOrderAsync(CarrierOrderRequest req, CancellationToken ct = default)
    {
        var token = _config["Ghn:Token"];
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("[GHN] Token not configured — using stub tracking code");
            return new CreateOrderResult(true, $"GHN-{Guid.NewGuid():N}"[..14].ToUpper(), null, "3-5 business days", null);
        }

        var payload = new
        {
            payment_type_id = 2,
            note = req.Note,
            to_name = req.RecipientName,
            to_phone = req.RecipientPhone,
            to_address = req.RecipientAddress,
            to_ward_name = req.RecipientWardName,
            to_district_name = req.RecipientDistrictName,
            to_province_name = req.RecipientProvinceName,
            cod_amount = 0,
            weight = (int)(req.Weight * 1000),
            insurance_value = (int)req.InsuranceValue,
            service_type_id = 2,
            items = req.Items.Select(i => new { name = i.Name, quantity = i.Quantity, price = (int)i.Price })
        };

        try
        {
            var httpReq = BuildRequest(HttpMethod.Post, "/v2/shipping-order/create", payload);
            var response = await _http.SendAsync(httpReq, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[GHN] CreateOrder failed: {Status} {Body}", response.StatusCode, json);
                return new CreateOrderResult(false, null, null, null, $"GHN error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var trackingCode = data.GetProperty("order_code").GetString() ?? string.Empty;
            var expectedDelivery = data.TryGetProperty("expected_delivery_time", out var et) ? et.GetString() : "3-5 days";

            _logger.LogInformation("[GHN] Order created: {TrackingCode}", trackingCode);
            return new CreateOrderResult(true, trackingCode, trackingCode, expectedDelivery, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GHN] CreateOrder exception");
            return new CreateOrderResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<TrackingResult> TrackOrderAsync(string trackingCode, CancellationToken ct = default)
    {
        var token = _config["Ghn:Token"];
        if (string.IsNullOrEmpty(token))
            return new TrackingResult(true, "unknown", "GHN not configured", new(), null);

        try
        {
            var payload = new { order_code = trackingCode };
            var httpReq = BuildRequest(HttpMethod.Post, "/v2/shipping-order/detail", payload);
            var response = await _http.SendAsync(httpReq, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new TrackingResult(false, "error", null, new(), $"GHN error: {response.StatusCode}");

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString() ?? "unknown";

            return new TrackingResult(true, status, null, new(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GHN] TrackOrder exception for {Code}", trackingCode);
            return new TrackingResult(false, "error", ex.Message, new(), ex.Message);
        }
    }

    public async Task<List<ShippingFeeResult>> CalculateFeeAsync(FeeRequest request, CancellationToken ct = default)
    {
        var token = _config["Ghn:Token"];
        if (string.IsNullOrEmpty(token))
            return new List<ShippingFeeResult> { new("Standard", 30000, "3-5 days"), new("Express", 50000, "1-2 days") };

        try
        {
            var payload = new
            {
                to_province_name = request.ProvinceName,
                to_district_name = request.DistrictName,
                weight = (int)(request.Weight * 1000),
                insurance_value = (int)request.InsuranceValue
            };

            var httpReq = BuildRequest(HttpMethod.Post, "/v2/shipping-order/available-services", payload);
            var response = await _http.SendAsync(httpReq, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new List<ShippingFeeResult>();

            using var doc = JsonDocument.Parse(json);
            var results = new List<ShippingFeeResult>();

            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var name = item.TryGetProperty("service_type_name", out var n) ? n.GetString() ?? "Service" : "Service";
                var fee = item.TryGetProperty("total_fee", out var f) ? f.GetDecimal() : 0;
                results.Add(new ShippingFeeResult(name, fee, "3-5 business days"));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GHN] CalculateFee exception");
            return new List<ShippingFeeResult>();
        }
    }
}

/// <summary>
/// Stub carrier for dev/test — returns fake tracking codes immediately.
/// </summary>
public class NullCarrierClient : ICarrierClient
{
    private readonly ILogger<NullCarrierClient> _logger;
    public NullCarrierClient(ILogger<NullCarrierClient> logger) => _logger = logger;

    public Task<CreateOrderResult> CreateOrderAsync(CarrierOrderRequest request, CancellationToken ct = default)
    {
        var code = "STUB-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
        _logger.LogInformation("[DEV] NullCarrier: CreateOrder → {Code}", code);
        return Task.FromResult(new CreateOrderResult(true, code, code, "3-5 days (stub)", null));
    }

    public Task<TrackingResult> TrackOrderAsync(string trackingCode, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV] NullCarrier: Track {Code}", trackingCode);
        return Task.FromResult(new TrackingResult(true, "InTransit", "Stub tracking", new(), null));
    }

    public Task<List<ShippingFeeResult>> CalculateFeeAsync(FeeRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new List<ShippingFeeResult>
        {
            new("Standard (Stub)", 30000, "3-5 days"),
            new("Express (Stub)", 50000, "1-2 days")
        });
    }
}
