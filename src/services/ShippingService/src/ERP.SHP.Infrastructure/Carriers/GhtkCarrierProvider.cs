using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.Carriers;

/// <summary>
/// GHTK (Giao Hang Tiet Kiem) e-logistic API. Checked against https://api.ghtk.vn/en/docs (2026-10-27):
/// headers <c>Token</c> (= account secret) and <c>X-Client-Source</c> (= partner code); fee GET /services/shipment/fee (weight in GRAMS);
/// create POST /services/shipment/order?ver=1.5 (product weights in KILOGRAMS, <c>pick_money</c> = COD in VND, <c>value</c> = declared value);
/// status GET /services/shipment/v2/{code}; cancel POST /services/shipment/cancel/{code} (only before pickup); webhook POST with
/// <c>label_id, partner_id, status_id, action_time, reason, pick_money…</c>. GHTK does not sign its webhook: the shop puts a secret in the callback
/// address it registers (<c>?hash=…</c>) and that secret is what is checked here.
/// Hosts: production https://services.giaohangtietkiem.vn, staging https://services-staging.ghtklab.com (configuration <c>Carriers:Ghtk:*</c>).
/// </summary>
public class GhtkCarrierProvider : CarrierProviderBase, ICarrierProvider
{
    public string Code => CarrierCodes.Ghtk;

    public GhtkCarrierProvider(IConfiguration config, ILogger<GhtkCarrierProvider> log, IHttpClientFactory http) : base(config, log, http) { }

    private string? Host(CarrierCredentials c) =>
        BaseUrl("Carriers:Ghtk", c.Sandbox, "https://services-staging.ghtklab.com", "https://services.giaohangtietkiem.vn");

    private HttpRequestMessage Request(CarrierCredentials c, HttpMethod method, string pathAndQuery)
    {
        var request = new HttpRequestMessage(method, Host(c) + pathAndQuery);
        request.Headers.Add("Token", c.Secret1);
        request.Headers.Add("X-Client-Source", c.PublicId);
        return request;
    }

    public async Task<CarrierQuote> QuoteAsync(CarrierCredentials c, CarrierQuoteRequest r, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            "pick_province=" + Uri.EscapeDataString(r.From.Province),
            "pick_district=" + Uri.EscapeDataString(r.From.District),
            "province=" + Uri.EscapeDataString(r.To.Province),
            "district=" + Uri.EscapeDataString(r.To.District),
            "weight=" + Math.Max(1, r.WeightGrams),
            "value=" + Vnd(r.DeclaredValue),
            "transport=road",
        };
        if (!string.IsNullOrWhiteSpace(r.To.Ward)) query.Add("ward=" + Uri.EscapeDataString(r.To.Ward));
        if (!string.IsNullOrWhiteSpace(r.To.Street)) query.Add("address=" + Uri.EscapeDataString(r.To.Street));

        var (reached, status, body, error) = await SendAsync(Request(c, HttpMethod.Get, "/services/shipment/fee?" + string.Join('&', query)), ct);
        if (!reached) return new CarrierQuote(Code, false, 0, null, null, error);

        var json = Parse(body);
        if (Str(json, "success") != "true")
            return new CarrierQuote(Code, false, 0, null, null, $"GHTK refused the quote: {Str(json, "message") ?? "HTTP " + status}");
        if (Str(json, "fee", "delivery") == "false")
            return new CarrierQuote(Code, false, 0, null, null, "GHTK does not deliver to this address");

        var fee = Dec(json, "fee", "fee");
        return fee is null
            ? new CarrierQuote(Code, false, 0, null, null, "GHTK answered without a fee")
            : new CarrierQuote(Code, true, fee.Value, Str(json, "fee", "name"), null, null);
    }

    public async Task<CarrierBooking> BookAsync(CarrierCredentials c, CarrierBookRequest r, CancellationToken ct = default)
    {
        var order = new Dictionary<string, object?>
        {
            ["id"] = r.OrderReference,
            ["pick_name"] = r.From.Name,
            ["pick_tel"] = r.From.Phone,
            ["pick_address"] = r.From.Street,
            ["pick_province"] = r.From.Province,
            ["pick_district"] = r.From.District,
            ["name"] = r.To.Name,
            ["tel"] = r.To.Phone,
            ["address"] = r.To.Street,
            ["province"] = r.To.Province,
            ["district"] = r.To.District,
            ["hamlet"] = "Other",
            ["pick_money"] = Vnd(r.CodAmount),
            ["value"] = Vnd(r.DeclaredValue),
            // The shop pays the carrier: the customer is charged no shipping fee yet, so the recipient only pays the COD amount.
            ["is_freeship"] = 1,
            ["weight_option"] = "gram",
            ["total_weight"] = Math.Max(1, r.WeightGrams),
            ["transport"] = "road",
            ["pick_option"] = "cod",
            ["note"] = Truncate(r.Note, 120),
        };
        if (!string.IsNullOrWhiteSpace(r.From.Ward)) order["pick_ward"] = r.From.Ward;
        if (!string.IsNullOrWhiteSpace(r.To.Ward)) order["ward"] = r.To.Ward;

        var payload = new
        {
            products = r.Items.Select(i => new { name = i.Name, weight = Math.Round(i.WeightGrams / 1000m, 3), quantity = i.Quantity, price = Vnd(i.UnitPrice) }),
            order
        };

        var request = Request(c, HttpMethod.Post, "/services/shipment/order?ver=1.5");
        request.Content = Json(payload);
        var (reached, status, body, error) = await SendAsync(request, ct);
        if (!reached) return new CarrierBooking(false, null, null, null, null, error);

        var json = Parse(body);
        if (Str(json, "success") == "true")
        {
            var label = Str(json, "order", "label");
            if (string.IsNullOrEmpty(label)) return new CarrierBooking(false, null, null, null, null, "GHTK accepted the order but returned no tracking code");
            return new CarrierBooking(true, label, Str(json, "order", "tracking_id"), Dec(json, "order", "fee"), ParseEta(Str(json, "order", "estimated_deliver_time")), null);
        }

        // The same order id again (a retry after a lost answer): the existing parcel is the answer, not a second one.
        if (Str(json, "error", "code") == "ORDER_ID_EXIST" && Str(json, "error", "ghtk_label") is { Length: > 0 } existing)
            return new CarrierBooking(true, existing, null, null, null, null);

        return new CarrierBooking(false, null, null, null, null, $"GHTK refused the order: {Str(json, "message") ?? "HTTP " + status}");
    }

    public async Task<CarrierCancelResult> CancelAsync(CarrierCredentials c, string trackingCode, string reason, CancellationToken ct = default)
    {
        var request = Request(c, HttpMethod.Post, "/services/shipment/cancel/" + Uri.EscapeDataString(trackingCode));
        request.Content = Json(new { });
        var (reached, status, body, error) = await SendAsync(request, ct);
        if (!reached) return new CarrierCancelResult(false, error);

        var json = Parse(body);
        return Str(json, "success") == "true"
            ? new CarrierCancelResult(true, null)
            : new CarrierCancelResult(false, $"GHTK did not cancel the order: {Str(json, "message") ?? "HTTP " + status}");
    }

    public async Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials c, string trackingCode, CancellationToken ct = default)
    {
        var (reached, status, body, error) = await SendAsync(Request(c, HttpMethod.Get, "/services/shipment/v2/" + Uri.EscapeDataString(trackingCode)), ct);
        if (!reached) return new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null, error);

        var json = Parse(body);
        if (Str(json, "success") != "true")
            return new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null, $"GHTK could not give the status: {Str(json, "message") ?? "HTTP " + status}");

        var code = Str(json, "order", "status") ?? string.Empty;
        DateTime? when = DateTime.TryParse(Str(json, "order", "modified"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var t) ? t : null;
        return new CarrierStatusResult(true, code, MapStatus(code), Str(json, "order", "status_text"), when, null);
    }

    public string? PeekTrackingCode(CarrierWebhookRequest request) => Fields(request)?.GetValueOrDefault("label_id");

    public CarrierWebhookEvent VerifyWebhook(CarrierCredentials c, CarrierWebhookRequest request)
    {
        if (!SecretMatches(c.WebhookSecret, Query(request, "hash")))
            return Reject("the callback secret (hash) is wrong or not set");

        var f = Fields(request);
        if (f == null || !f.TryGetValue("label_id", out var label) || string.IsNullOrWhiteSpace(label) || !f.TryGetValue("status_id", out var statusId))
            return Reject("the body is not a GHTK notification");

        var when = DateTime.TryParse(f.GetValueOrDefault("action_time"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
            ? t : DateTime.UtcNow;
        decimal? cod = decimal.TryParse(f.GetValueOrDefault("pick_money"), NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : null;
        return new CarrierWebhookEvent(true, null, label, statusId, MapStatus(statusId), when, f.GetValueOrDefault("reason"), cod);
    }

    public CarrierWebhookReply WebhookReply(bool accepted, string message) =>
        accepted ? new CarrierWebhookReply(200, new { success = true }) : new CarrierWebhookReply(400, new { success = false, message });

    private static CarrierWebhookEvent Reject(string why) =>
        new(false, why, null, string.Empty, CarrierShipmentStatus.Accepted, DateTime.UtcNow, null, null);

    /// <summary>The notification's fields, from JSON or from a form body (the docs show both).</summary>
    private static Dictionary<string, string>? Fields(CarrierWebhookRequest request)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body)) return request.Query.Count > 0 ? request.Query.ToDictionary(p => p.Key, p => p.Value) : null;

        if (body.StartsWith('{'))
        {
            var json = Parse(body);
            if (json is not { ValueKind: JsonValueKind.Object } obj) return null;
            return obj.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText());
        }

        return body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : string.Empty);
    }

    /// <summary>
    /// GHTK status ids (docs): -1 cancelled, 1 not received, 2 received, 3 picked up, 4 delivering, 5 delivered, 6 reconciled, 7 pickup failed, 8 pickup delayed,
    /// 9 delivery failed, 10 delivery delayed, 11 return reconciled, 12 picking up, 13 compensation, 20 returning, 21 returned.
    /// 45 / 49 / 123 / 127 / 128 / 410 are what the SHIPPER typed (the docs say they are not official): a tracking line only.
    /// </summary>
    public static CarrierShipmentStatus MapStatus(string? statusId) => statusId?.Trim() switch
    {
        "-1" => CarrierShipmentStatus.Cancelled,
        "3" => CarrierShipmentStatus.PickedUp,
        "4" => CarrierShipmentStatus.OutForDelivery,
        "5" or "6" => CarrierShipmentStatus.Delivered,
        "9" => CarrierShipmentStatus.DeliveryFailed,
        "20" => CarrierShipmentStatus.Returning,
        "21" or "11" => CarrierShipmentStatus.Returned,
        _ => CarrierShipmentStatus.Accepted
    };

    /// <summary>"Sang 2026-10-30" / "14h 2026-10-30": the date part is what matters.</summary>
    private static DateTime? ParseEta(string? text)
    {
        var m = Regex.Match(text ?? string.Empty, @"\d{4}-\d{2}-\d{2}");
        return m.Success && DateTime.TryParse(m.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;
    }
}
