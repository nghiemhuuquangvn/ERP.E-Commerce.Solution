using System.Net;
using System.Text.Json;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Infrastructure.Gateway;
using FluentAssertions;
using Xunit;
using static ERP.PAY.UnitTests.Gateways.GatewayTestSupport;

namespace ERP.PAY.UnitTests.Gateways;

/// <summary>ROADMAP P1-10: ZaloPay create / callback / browser return / refund, with MACs computed independently of the code under test.</summary>
public class ZaloPayGatewayTests
{
    private const string App = "2553", Key1 = "KEY1", Key2 = "KEY2";
    private static readonly GatewayCredentials Creds = new("ZaloPay", App, null, Key1, Key2, true);
    private static readonly DateTime Now = new(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc);   // 10:00 in Vietnam

    private static ZaloPayGateway Gateway(IHttpClientFactory http) => new(Config(), Log<ZaloPayGateway>(), http);

    private static GatewayPaymentRequest Request(string currency = "VND") =>
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"), 80000m, currency, "261027_1111111122100000", "Order SO-2",
            "https://shop.test/api/v1/payment-gateway/callback/zalopay", "https://shop.test/api/v1/payment-gateway/ipn/zalopay", Now);

    private static Dictionary<string, string> Form(string body) =>
        body.Split('&').Select(p => p.Split('=')).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));

    [Fact]
    public void The_transaction_reference_starts_with_the_vietnam_date_and_differs_per_attempt()
    {
        var gw = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var first = gw.NewTransactionRef(id, Now);
        var later = gw.NewTransactionRef(id, Now.AddMinutes(5));

        first.Should().Be("261027_1111111122100000000");
        later.Should().NotBe(first);
        gw.NewTransactionRef(id, Now.AddMilliseconds(5)).Should().NotBe(first);   // two attempts in the same second
        first.Length.Should().BeLessThan(40);
    }

    [Fact]
    public async Task Create_signs_the_pipe_separated_fields_with_key1_and_returns_the_order_url()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{\"return_code\":1,\"return_message\":\"ok\",\"order_url\":\"https://sbgateway.zalopay.vn/openinapp?order=xyz\"}"));

        var result = await Gateway(http).CreatePaymentAsync(Creds, Request());

        result.Success.Should().BeTrue();
        result.PaymentUrl.Should().Be("https://sbgateway.zalopay.vn/openinapp?order=xyz");
        var (message, body) = seen.Single();
        message.RequestUri!.ToString().Should().Be("https://sb-openapi.zalopay.vn/v2/create");

        var form = Form(body);
        form["amount"].Should().Be("80000");
        form["callback_url"].Should().Be("https://shop.test/api/v1/payment-gateway/ipn/zalopay");
        JsonDocument.Parse(form["embed_data"]).RootElement.GetProperty("redirecturl").GetString()
            .Should().Be("https://shop.test/api/v1/payment-gateway/callback/zalopay");
        var data = $"{App}|{form["app_trans_id"]}|{form["app_user"]}|80000|{form["app_time"]}|{form["embed_data"]}|{form["item"]}";
        form["mac"].Should().Be(Sha256Hex(Key1, data));
    }

    [Fact]
    public async Task Create_refuses_a_foreign_currency_and_reports_a_zalopay_error()
    {
        var (never, seen) = Http((_, _) => (HttpStatusCode.OK, "{}"));
        (await Gateway(never).CreatePaymentAsync(Creds, Request("USD"))).Error.Should().Contain("VND");
        seen.Should().BeEmpty();

        var (declined, _) = Http((_, _) => (HttpStatusCode.OK, "{\"return_code\":2,\"return_message\":\"bad mac\"}"));
        (await Gateway(declined).CreatePaymentAsync(Creds, Request())).Error.Should().Contain("bad mac");
    }

    // ── callback (server to server) ───────────────────────────────────────

    private static string CallbackBody(string appTransId = "261027_1111111122100000", long amount = 80000, string? macOverride = null, string app = App)
    {
        var data = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["app_id"] = long.Parse(app), ["app_trans_id"] = appTransId, ["amount"] = amount, ["zp_trans_id"] = 220101000123, ["app_user"] = "erp-11111111"
        });
        return JsonSerializer.Serialize(new Dictionary<string, object> { ["data"] = data, ["mac"] = macOverride ?? Sha256Hex(Key2, data), ["type"] = 1 });
    }

    [Fact]
    public void A_correctly_signed_callback_is_a_paid_notification_with_amount_and_zalopay_transaction_id()
    {
        var verify = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory)
            .Verify(Creds, new GatewayCallback("POST", new Dictionary<string, string>(), CallbackBody()));

        verify.Should().BeEquivalentTo(new GatewayVerifyResult(true, true, "261027_1111111122100000", "220101000123", null, 80000m));
    }

    [Fact]
    public void A_callback_signed_with_the_wrong_key_or_for_another_app_or_with_changed_data_is_refused()
    {
        var gw = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);
        GatewayVerifyResult Check(string body, GatewayCredentials? c = null) => gw.Verify(c ?? Creds, new GatewayCallback("POST", new Dictionary<string, string>(), body));

        Check(CallbackBody(), Creds with { Secret2 = "other" }).Success.Should().BeFalse();
        Check(CallbackBody(macOverride: "00")).Success.Should().BeFalse();
        Check(CallbackBody(app: "9999")).Success.Should().BeFalse();                       // right MAC, someone else's app_id
        Check(CallbackBody().Replace("80000", "100")).Success.Should().BeFalse();           // data changed after signing
        Check("not json").Success.Should().BeFalse();
    }

    [Fact]
    public void The_callback_is_answered_with_return_code_1_and_the_reference_can_be_peeked()
    {
        var gw = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);

        gw.PeekTransactionRef(new GatewayCallback("POST", new Dictionary<string, string>(), CallbackBody("261027_REF"))).Should().Be("261027_REF");
        var reply = gw.IpnResponse(true, "ok");
        reply.StatusCode.Should().Be(200);
        ((Dictionary<string, object>)reply.Body!)["return_code"].Should().Be(1);
        ((Dictionary<string, object>)gw.IpnResponse(false, "no").Body!)["return_code"].Should().Be(-1);
    }

    // ── browser return ────────────────────────────────────────────────────

    private static GatewayCallback ReturnQuery(string status, string? checksumOverride = null, string amount = "80000")
    {
        var q = new Dictionary<string, string>
        {
            ["appid"] = App, ["apptransid"] = "261027_REF", ["pmcid"] = "38", ["bankcode"] = "", ["amount"] = amount, ["discountamount"] = "0", ["status"] = status
        };
        q["checksum"] = checksumOverride ?? Sha256Hex(Key2, $"{App}|261027_REF|38||{amount}|0|{status}");
        return new GatewayCallback("GET", q, string.Empty);
    }

    [Fact]
    public void The_browser_return_is_verified_with_key2_and_status_1_means_paid()
    {
        var gw = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);

        gw.Verify(Creds, ReturnQuery("1")).Should().BeEquivalentTo(new GatewayVerifyResult(true, true, "261027_REF", null, null, 80000m));
        var cancelled = gw.Verify(Creds, ReturnQuery("-49"));
        cancelled.Success.Should().BeTrue();
        cancelled.IsPaid.Should().BeFalse();
        gw.Verify(Creds, ReturnQuery("1", checksumOverride: "bad")).Success.Should().BeFalse();
        gw.Verify(Creds, ReturnQuery("1", amount: "80000").Query.ToDictionary(k => k.Key, k => k.Key == "amount" ? "1" : k.Value) is var forged
            ? new GatewayCallback("GET", forged, string.Empty) : throw new InvalidOperationException()).Success.Should().BeFalse();
    }

    // ── refund ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(3, false, true)]    // processing: neither a failure nor confirmed money
    [InlineData(2, false, false)]
    public async Task Refund_maps_the_zalopay_return_code_and_signs_with_key1(int code, bool success, bool pending)
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, $"{{\"return_code\":{code},\"return_message\":\"m\",\"refund_id\":220101999}}"));

        var result = await Gateway(http).RefundAsync(Creds, "220101000123", "261027_REF", 30000m, "returned");

        (result.Success, result.Pending).Should().Be((success, pending));
        var form = Form(seen.Single().Body);
        form["zp_trans_id"].Should().Be("220101000123");
        form["mac"].Should().Be(Sha256Hex(Key1, $"{App}|220101000123|30000|returned|{form["timestamp"]}"));
    }

    [Fact]
    public async Task Refund_needs_the_zalopay_transaction_id()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{}"));

        (await Gateway(http).RefundAsync(Creds, "", "R", 1m, "x")).Success.Should().BeFalse();
        seen.Should().BeEmpty();
    }
}
