using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Domain.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.Carriers;

/// <summary>
/// Viettel Post partner API v2.
/// <b>NOT verified against the official documentation</b> (it is behind a partner login: https://api.viettelpost.vn answers 403 to the public): paths,
/// field names and the order-payment codes below are written from the integration notes available to the author and must be confirmed with a
/// Viettel Post sandbox account before real use. Every refusal by the carrier is reported as an error (the shipment then goes to the next candidate),
/// never as a success, and delivery is only inferred from a status that says so in words (see <see cref="MapStatus"/>), because delivery captures the COD payment.
/// Shape: login POST /user/Login {USERNAME, PASSWORD} → token (cached, sent as header <c>Token</c>); places are numeric ids found by name through
/// /categories/listProvinceById, /listDistrict, /listWards (cached); POST /order/getPrice; POST /order/createOrder (MONEY_COLLECTION = COD);
/// POST /order/UpdateOrder TYPE 4 = cancel; webhook {DATA:{ORDER_NUMBER, ORDER_STATUS, ORDER_STATUSDATE, STATUS_NAME, NOTE, MONEY_COLLECTION}, TOKEN}.
/// Hosts: production https://partner.viettelpost.vn/v2, sandbox https://partnerdev.viettelpost.vn/v2 (configuration <c>Carriers:Vtp:*</c>).
/// </summary>
public class ViettelPostCarrierProvider : CarrierProviderBase, ICarrierProvider
{
    private static readonly ConcurrentDictionary<string, (string Token, DateTime Expires)> Tokens = new();
    private static readonly ConcurrentDictionary<string, List<(long Id, string Name)>> Places = new();

    public string Code => CarrierCodes.ViettelPost;

    public ViettelPostCarrierProvider(IConfiguration config, ILogger<ViettelPostCarrierProvider> log, IHttpClientFactory http) : base(config, log, http) { }

    private string Host(CarrierCredentials c) =>
        BaseUrl("Carriers:Vtp", c.Sandbox, "https://partnerdev.viettelpost.vn/v2", "https://partner.viettelpost.vn/v2")!;

    private async Task<(string? Token, string? Error)> TokenAsync(CarrierCredentials c, bool refresh, CancellationToken ct)
    {
        var key = $"{Host(c)}|{c.Username}";
        if (!refresh && Tokens.TryGetValue(key, out var cached) && cached.Expires > DateTime.UtcNow) return (cached.Token, null);

        var request = new HttpRequestMessage(HttpMethod.Post, Host(c) + "/user/Login") { Content = Json(new { USERNAME = c.Username, PASSWORD = c.Secret1 }) };
        var (reached, status, body, error) = await SendAsync(request, ct);
        if (!reached) return (null, error);

        var token = Str(Parse(body), "data", "token");
        if (string.IsNullOrEmpty(token)) return (null, $"Viettel Post refused the login: {Str(Parse(body), "message") ?? "HTTP " + status}");

        Tokens[key] = (token, DateTime.UtcNow.AddHours(6));
        return (token, null);
    }

    /// <summary>One authenticated call; a rejected token is renewed once.</summary>
    private async Task<(bool Ok, string Body, string? Error)> CallAsync(CarrierCredentials c, HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var (token, loginError) = await TokenAsync(c, refresh: attempt > 0, ct);
            if (token == null) return (false, string.Empty, loginError);

            var request = new HttpRequestMessage(method, Host(c) + path);
            request.Headers.Add("Token", token);
            if (payload != null) request.Content = Json(payload);
            var (reached, status, body, error) = await SendAsync(request, ct);
            if (!reached) return (false, string.Empty, error);
            if (status == 401 && attempt == 0) continue;
            return (true, body, null);
        }
        return (false, string.Empty, "Viettel Post kept refusing the token");
    }

    // ── places: names → ids ──────────────────────────────────────────────

    private async Task<List<(long Id, string Name)>> PlacesAsync(CarrierCredentials c, string path, string idField, string nameField, CancellationToken ct)
    {
        var key = Host(c) + path;
        if (Places.TryGetValue(key, out var cached)) return cached;

        var (ok, body, _) = await CallAsync(c, HttpMethod.Get, path, null, ct);
        var list = new List<(long, string)>();
        if (ok && Parse(body) is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
                if (item.TryGetProperty(idField, out var id) && id.TryGetInt64(out var idValue) && item.TryGetProperty(nameField, out var name))
                    list.Add((idValue, name.GetString() ?? string.Empty));

        if (list.Count > 0) Places[key] = list;   // never cache an empty answer: the next call retries
        return list;
    }

    /// <summary>The numeric ids of an address's province, district and ward, found by name. Null (with the reason) when a name does not match.</summary>
    internal async Task<((long Province, long District, long Ward)? Ids, string? Error)> ResolveAsync(CarrierCredentials c, string province, string district, string? ward, CancellationToken ct)
    {
        var provinces = await PlacesAsync(c, "/categories/listProvinceById?provinceId=-1", "PROVINCE_ID", "PROVINCE_NAME", ct);
        var p = Find(provinces, province);
        if (p == null) return (null, $"Viettel Post does not know the province '{province}'");

        var districts = await PlacesAsync(c, $"/categories/listDistrict?provinceId={p.Value.Id}", "DISTRICT_ID", "DISTRICT_NAME", ct);
        var d = Find(districts, district);
        if (d == null) return (null, $"Viettel Post does not know the district '{district}' in {province}");

        long wardId = 0;
        if (!string.IsNullOrWhiteSpace(ward))
        {
            var wards = await PlacesAsync(c, $"/categories/listWards?districtId={d.Value.Id}", "WARDS_ID", "WARDS_NAME", ct);
            wardId = Find(wards, ward)?.Id ?? 0;
        }
        return ((p.Value.Id, d.Value.Id, wardId), null);
    }

    internal static (long Id, string Name)? Find(IEnumerable<(long Id, string Name)> places, string name)
    {
        var wanted = VnText.Normalize(name);
        if (wanted.Length == 0) return null;
        var exact = places.Where(p => VnText.Normalize(p.Name) == wanted).ToList();
        return exact.Count == 1 ? exact[0] : null;
    }

    // ── API ─────────────────────────────────────────────────────────────

    public async Task<CarrierQuote> QuoteAsync(CarrierCredentials c, CarrierQuoteRequest r, CancellationToken ct = default)
    {
        var from = await ResolveAsync(c, r.From.Province, r.From.District, r.From.Ward, ct);
        if (from.Ids == null) return new CarrierQuote(Code, false, 0, null, null, from.Error);
        var to = await ResolveAsync(c, r.To.Province, r.To.District, r.To.Ward, ct);
        if (to.Ids == null) return new CarrierQuote(Code, false, 0, null, null, to.Error);

        var (ok, body, error) = await CallAsync(c, HttpMethod.Post, "/order/getPrice", new
        {
            PRODUCT_WEIGHT = Math.Max(1, r.WeightGrams),
            PRODUCT_PRICE = Vnd(r.DeclaredValue),
            MONEY_COLLECTION = Vnd(r.CodAmount),
            ORDER_SERVICE = "VCN",
            SENDER_PROVINCE = from.Ids.Value.Province,
            SENDER_DISTRICT = from.Ids.Value.District,
            RECEIVER_PROVINCE = to.Ids.Value.Province,
            RECEIVER_DISTRICT = to.Ids.Value.District,
            PRODUCT_TYPE = "HH",
            NATIONAL_TYPE = 1,
        }, ct);
        if (!ok) return new CarrierQuote(Code, false, 0, null, null, error);

        var json = Parse(body);
        var fee = Dec(json, "data", "MONEY_TOTAL");
        return fee is > 0
            ? new CarrierQuote(Code, true, fee.Value, "VCN", null, null)
            : new CarrierQuote(Code, false, 0, null, null, $"Viettel Post could not price the parcel: {Str(json, "message") ?? "no fee in the answer"}");
    }

    public async Task<CarrierBooking> BookAsync(CarrierCredentials c, CarrierBookRequest r, CancellationToken ct = default)
    {
        var from = await ResolveAsync(c, r.From.Province, r.From.District, r.From.Ward, ct);
        if (from.Ids == null) return new CarrierBooking(false, null, null, null, null, from.Error);
        var to = await ResolveAsync(c, r.To.Province, r.To.District, r.To.Ward, ct);
        if (to.Ids == null) return new CarrierBooking(false, null, null, null, null, to.Error);

        var cod = Vnd(r.CodAmount);
        var payload = new Dictionary<string, object?>
        {
            ["ORDER_NUMBER"] = Truncate(r.OrderReference, 50),
            ["SENDER_FULLNAME"] = r.From.Name,
            ["SENDER_ADDRESS"] = r.From.Street,
            ["SENDER_PHONE"] = r.From.Phone,
            ["SENDER_PROVINCE"] = from.Ids.Value.Province,
            ["SENDER_DISTRICT"] = from.Ids.Value.District,
            ["SENDER_WARD"] = from.Ids.Value.Ward,
            ["RECEIVER_FULLNAME"] = r.To.Name,
            ["RECEIVER_ADDRESS"] = r.To.Street,
            ["RECEIVER_PHONE"] = r.To.Phone,
            ["RECEIVER_PROVINCE"] = to.Ids.Value.Province,
            ["RECEIVER_DISTRICT"] = to.Ids.Value.District,
            ["RECEIVER_WARD"] = to.Ids.Value.Ward,
            ["PRODUCT_NAME"] = Truncate(string.Join(", ", r.Items.Select(i => i.Name)), 200),
            ["PRODUCT_DESCRIPTION"] = Truncate(r.Note, 200),
            ["PRODUCT_QUANTITY"] = r.Items.Sum(i => Math.Max(1, i.Quantity)),
            ["PRODUCT_PRICE"] = Vnd(r.DeclaredValue),
            ["PRODUCT_WEIGHT"] = Math.Max(1, r.WeightGrams),
            ["PRODUCT_TYPE"] = "HH",
            // 3 = the recipient pays the goods only (cash on delivery), 1 = nothing collected; the shop pays the carrier's fee.
            ["ORDER_PAYMENT"] = cod > 0 ? 3 : 1,
            ["ORDER_SERVICE"] = "VCN",
            ["ORDER_NOTE"] = Truncate(r.Note, 200),
            ["MONEY_COLLECTION"] = cod,
            ["LIST_ITEM"] = r.Items.Select(i => new { PRODUCT_NAME = Truncate(i.Name, 200), PRODUCT_QUANTITY = Math.Max(1, i.Quantity), PRODUCT_PRICE = Vnd(i.UnitPrice), PRODUCT_WEIGHT = Math.Max(1, i.WeightGrams) }).ToList(),
        };

        var (ok, body, error) = await CallAsync(c, HttpMethod.Post, "/order/createOrder", payload, ct);
        if (!ok) return new CarrierBooking(false, null, null, null, null, error);

        var json = Parse(body);
        var number = Str(json, "data", "ORDER_NUMBER");
        if (Str(json, "error") is "false" or null && !string.IsNullOrEmpty(number))
            return new CarrierBooking(true, number, number, Dec(json, "data", "MONEY_TOTAL"), null, null);
        return new CarrierBooking(false, null, null, null, null, $"Viettel Post refused the order: {Str(json, "message") ?? "no order number in the answer"}");
    }

    public async Task<CarrierCancelResult> CancelAsync(CarrierCredentials c, string trackingCode, string reason, CancellationToken ct = default)
    {
        var (ok, body, error) = await CallAsync(c, HttpMethod.Post, "/order/UpdateOrder", new { TYPE = 4, ORDER_NUMBER = trackingCode, NOTE = Truncate(reason, 200) }, ct);
        if (!ok) return new CarrierCancelResult(false, error);

        var json = Parse(body);
        return Str(json, "error") == "false" || Str(json, "status") == "200"
            ? new CarrierCancelResult(true, null)
            : new CarrierCancelResult(false, $"Viettel Post did not cancel the order: {Str(json, "message") ?? "no reason given"}");
    }

    public Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials c, string trackingCode, CancellationToken ct = default) =>
        Task.FromResult(new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null,
            "Viettel Post reports its statuses through its webhook; there is no polling call in this integration"));

    public string? PeekTrackingCode(CarrierWebhookRequest request) => Str(Parse(request.Body), "DATA", "ORDER_NUMBER");

    public CarrierWebhookEvent VerifyWebhook(CarrierCredentials c, CarrierWebhookRequest request)
    {
        var json = Parse(request.Body);
        // The shared secret travels in the payload (TOKEN) or in the callback address (?hash=), whichever the shop configured.
        if (!SecretMatches(c.WebhookSecret, Str(json, "TOKEN")) && !SecretMatches(c.WebhookSecret, Query(request, "hash")))
            return Reject("the callback secret (TOKEN / hash) is wrong or not set");

        var number = Str(json, "DATA", "ORDER_NUMBER");
        var code = Str(json, "DATA", "ORDER_STATUS");
        if (string.IsNullOrEmpty(number) || string.IsNullOrEmpty(code)) return Reject("the body is not a Viettel Post notification");

        var name = Str(json, "DATA", "STATUS_NAME");
        var when = DateTime.TryParseExact(Str(json, "DATA", "ORDER_STATUSDATE"), new[] { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var t) ? t : DateTime.UtcNow;
        return new CarrierWebhookEvent(true, null, number, code, MapStatus(code, name), when, Str(json, "DATA", "NOTE") ?? name, Dec(json, "DATA", "MONEY_COLLECTION"));
    }

    public CarrierWebhookReply WebhookReply(bool accepted, string message) =>
        accepted ? new CarrierWebhookReply(200, new { status = 200, error = false, message = "OK" }) : new CarrierWebhookReply(400, new { status = 400, error = true, message });

    private static CarrierWebhookEvent Reject(string why) =>
        new(false, why, null, string.Empty, CarrierShipmentStatus.Accepted, DateTime.UtcNow, null, null);

    /// <summary>
    /// Status code and Vietnamese status name. The numeric table is not verified, so the NAME decides what matters: a parcel counts as delivered only when
    /// its status says delivery succeeded ("giao hàng thành công"), because delivery captures the COD payment. Anything not recognised is only a tracking line.
    /// </summary>
    public static CarrierShipmentStatus MapStatus(string? code, string? name)
    {
        var text = " " + VnText.Normalize(name) + " ";
        // whole words only: "huy" (cancelled) must not match inside "chuyen" (transfer)
        bool Has(string part) => text.Contains(" " + part + " ", StringComparison.Ordinal);

        if (Has("khong thanh cong") || Has("that bai") || Has("khong giao duoc") || Has("giao khong")) return CarrierShipmentStatus.DeliveryFailed;
        if (Has("huy")) return CarrierShipmentStatus.Cancelled;
        if (Has("da tra hang") || Has("hoan thanh cong") || Has("da hoan") || Has("tra hang cho nguoi gui")) return CarrierShipmentStatus.Returned;
        if (Has("tra hang") || Has("hoan hang") || Has("chuyen hoan")) return CarrierShipmentStatus.Returning;
        if (Has("giao hang thanh cong") || Has("da giao") || Has("phat thanh cong")) return CarrierShipmentStatus.Delivered;
        if (Has("dang giao") || Has("dang phat") || Has("giao bua ta") || Has("di phat")) return CarrierShipmentStatus.OutForDelivery;
        if (Has("van chuyen") || Has("trung chuyen") || Has("den buu cuc") || Has("khai thac") || Has("roi buu cuc") || Has("luan chuyen")) return CarrierShipmentStatus.InTransit;
        if (Has("da lay hang") || Has("lay hang thanh cong") || Has("da nhan hang") || Has("nhan hang thanh cong")) return CarrierShipmentStatus.PickedUp;
        return CarrierShipmentStatus.Accepted;
    }
}
