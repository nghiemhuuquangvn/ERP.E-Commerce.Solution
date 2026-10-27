using System.Globalization;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.Carriers;

/// <summary>
/// GHN (Giao Hang Nhanh) API v2, now per shop. Checked against https://developer.ghn.vn/en/docs (2026-10-27): headers <c>Token</c> and <c>ShopId</c>;
/// create POST /v2/shipping-order/create takes place NAMES (province / district / ward), <c>cod_amount</c> (max 50,000,000), <c>insurance_value</c>
/// (max 5,000,000), <c>payment_type_id</c> 1 = the shop pays the fee, weight in grams, <c>client_order_code</c> makes a retry return the same order;
/// cancel POST /v2/switch-status/cancel; webhook (PascalCase: OrderCode, Status, Time, CODAmount, Reason…) is not signed — the shop puts a secret in the
/// callback address (<c>?hash=…</c>) and that is what is checked here. The fee API needs district ids and ward codes, which this service does not hold,
/// so GHN does not take part in the cheapest-quote comparison: it is reached through selection rules. The order-detail endpoint
/// (POST /v2/shipping-order/detail) is used for polling but was not on the pages read; its answer is treated defensively.
/// </summary>
public class GhnCarrierProvider : CarrierProviderBase, ICarrierProvider
{
    private const decimal MaxCod = 50_000_000m;
    private const decimal MaxInsurance = 5_000_000m;

    public string Code => CarrierCodes.Ghn;

    public GhnCarrierProvider(IConfiguration config, ILogger<GhnCarrierProvider> log, IHttpClientFactory http) : base(config, log, http) { }

    private HttpRequestMessage Request(CarrierCredentials c, string path, object body)
    {
        var host = BaseUrl("Carriers:Ghn", c.Sandbox, "https://dev-online-gateway.ghn.vn/shiip/public-api", "https://online-gateway.ghn.vn/shiip/public-api");
        var request = new HttpRequestMessage(HttpMethod.Post, host + path) { Content = Json(body) };
        request.Headers.Add("Token", c.Secret1);
        request.Headers.Add("ShopId", c.PublicId);
        return request;
    }

    public Task<CarrierQuote> QuoteAsync(CarrierCredentials c, CarrierQuoteRequest r, CancellationToken ct = default) =>
        Task.FromResult(new CarrierQuote(Code, false, 0, null, null,
            "GHN prices by district id and ward code, which this shop's addresses do not carry: use a selection rule to send orders to GHN"));

    public async Task<CarrierBooking> BookAsync(CarrierCredentials c, CarrierBookRequest r, CancellationToken ct = default)
    {
        if (r.CodAmount > MaxCod) return new CarrierBooking(false, null, null, null, null, $"GHN collects at most {MaxCod:N0} VND on delivery");

        var payload = new Dictionary<string, object?>
        {
            ["payment_type_id"] = 1,
            ["required_note"] = "CHOXEMHANGKHONGTHU",
            ["client_order_code"] = Truncate(r.OrderReference, 50),
            ["note"] = Truncate(r.Note, 500),
            ["to_name"] = r.To.Name,
            ["to_phone"] = r.To.Phone,
            ["to_address"] = r.To.Street,
            ["to_ward_name"] = r.To.Ward ?? string.Empty,
            ["to_district_name"] = r.To.District,
            ["to_province_name"] = r.To.Province,
            ["cod_amount"] = Vnd(r.CodAmount),
            ["insurance_value"] = Vnd(Math.Min(r.DeclaredValue, MaxInsurance)),
            ["service_type_id"] = 2,
            ["weight"] = Math.Max(1, r.WeightGrams),
            ["content"] = Truncate(string.Join(", ", r.Items.Select(i => $"{i.Name} x{i.Quantity}")), 2000),
            ["items"] = r.Items.Select(i => new { name = Truncate(i.Name, 512), quantity = Math.Max(1, i.Quantity), price = Vnd(i.UnitPrice), weight = Math.Max(1, i.WeightGrams) }).ToList(),
        };

        var (reached, status, body, error) = await SendAsync(Request(c, "/v2/shipping-order/create", payload), ct);
        if (!reached) return new CarrierBooking(false, null, null, null, null, error);

        var json = Parse(body);
        var code = Str(json, "data", "order_code");
        if (Str(json, "code") == "200" && !string.IsNullOrEmpty(code))
        {
            DateTime? eta = DateTime.TryParse(Str(json, "data", "expected_delivery_time"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;
            return new CarrierBooking(true, code, code, Dec(json, "data", "total_fee"), eta, null);
        }
        return new CarrierBooking(false, null, null, null, null, $"GHN refused the order: {Str(json, "code_message_value") ?? Str(json, "message") ?? "HTTP " + status}");
    }

    public async Task<CarrierCancelResult> CancelAsync(CarrierCredentials c, string trackingCode, string reason, CancellationToken ct = default)
    {
        var (reached, status, body, error) = await SendAsync(
            Request(c, "/v2/switch-status/cancel", new { order_codes = new[] { trackingCode }, reason_code = "GHN-CANCEL-OTHER", reason = Truncate(reason, 200) }), ct);
        if (!reached) return new CarrierCancelResult(false, error);

        var json = Parse(body);
        if (Str(json, "code") != "200") return new CarrierCancelResult(false, $"GHN did not cancel the order: {Str(json, "message") ?? "HTTP " + status}");

        // A 200 can still carry result:false for an order that can no longer be cancelled.
        var first = json is { ValueKind: System.Text.Json.JsonValueKind.Object } o && o.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Array && data.GetArrayLength() > 0
            ? (System.Text.Json.JsonElement?)data[0] : null;
        return Str(first, "result") == "true"
            ? new CarrierCancelResult(true, null)
            : new CarrierCancelResult(false, $"GHN did not cancel the order: {Str(first, "message") ?? "it can no longer be cancelled"}");
    }

    public async Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials c, string trackingCode, CancellationToken ct = default)
    {
        var (reached, status, body, error) = await SendAsync(Request(c, "/v2/shipping-order/detail", new { order_code = trackingCode }), ct);
        if (!reached) return new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null, error);

        var json = Parse(body);
        var s = Str(json, "data", "status");
        if (Str(json, "code") != "200" || string.IsNullOrEmpty(s))
            return new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null, $"GHN could not give the status: {Str(json, "message") ?? "HTTP " + status}");
        return new CarrierStatusResult(true, s, MapStatus(s), s, null, null);
    }

    public string? PeekTrackingCode(CarrierWebhookRequest request) => Str(Parse(request.Body), "OrderCode");

    public CarrierWebhookEvent VerifyWebhook(CarrierCredentials c, CarrierWebhookRequest request)
    {
        if (!SecretMatches(c.WebhookSecret, Query(request, "hash")))
            return Reject("the callback secret (hash) is wrong or not set");

        var json = Parse(request.Body);
        var code = Str(json, "OrderCode");
        var s = Str(json, "Status");
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(s)) return Reject("the body is not a GHN notification");

        var when = DateTime.TryParse(Str(json, "Time"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : DateTime.UtcNow;
        return new CarrierWebhookEvent(true, null, code, s, MapStatus(s), when, Str(json, "Reason") ?? Str(json, "Description"), Dec(json, "CODAmount"));
    }

    public CarrierWebhookReply WebhookReply(bool accepted, string message) =>
        accepted ? new CarrierWebhookReply(200, new { code = 200, message = "Success" }) : new CarrierWebhookReply(400, new { code = 400, message });

    private static CarrierWebhookEvent Reject(string why) =>
        new(false, why, null, string.Empty, CarrierShipmentStatus.Accepted, DateTime.UtcNow, null, null);

    /// <summary>GHN order status names (docs "Order Status Codes"; the page was not readable, so unknown names are only a tracking line).</summary>
    public static CarrierShipmentStatus MapStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "picked" or "storing" or "transporting" or "sorting" => CarrierShipmentStatus.InTransit,
        "delivering" or "money_collect_delivering" => CarrierShipmentStatus.OutForDelivery,
        "delivered" => CarrierShipmentStatus.Delivered,
        "delivery_fail" => CarrierShipmentStatus.DeliveryFailed,
        "waiting_to_return" or "return" or "return_transporting" or "return_sorting" or "returning" or "return_fail" => CarrierShipmentStatus.Returning,
        "returned" => CarrierShipmentStatus.Returned,
        "cancel" => CarrierShipmentStatus.Cancelled,
        _ => CarrierShipmentStatus.Accepted   // ready_to_pick, picking, money_collect_picking, exception, damage, lost, ...
    };
}
