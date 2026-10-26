using System.Net;
using System.Text.Json;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Infrastructure.Gateway;
using FluentAssertions;
using Xunit;
using static ERP.PAY.UnitTests.Gateways.GatewayTestSupport;

namespace ERP.PAY.UnitTests.Gateways;

/// <summary>ROADMAP P1-10: MoMo create / notification / refund, with signatures computed independently of the code under test.</summary>
public class MoMoGatewayTests
{
    private const string Partner = "MOMOTEST", Access = "ACCESSKEY", Secret = "SECRETKEY";
    private static readonly GatewayCredentials Creds = new("MoMo", Partner, Access, Secret, null, true);
    private static readonly DateTime Now = new(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc);

    private static MoMoGateway Gateway(IHttpClientFactory http, params (string, string)[] config) =>
        new(Config(config), Log<MoMoGateway>(), http);

    private static GatewayPaymentRequest Request(string currency = "VND") =>
        new(Guid.NewGuid(), 150000m, currency, "MREF1", "Order SO-1", "https://shop.test/api/v1/payment-gateway/callback/momo",
            "https://shop.test/api/v1/payment-gateway/ipn/momo", Now);

    // ── create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_signs_the_documented_field_list_in_alphabetical_order_and_returns_the_pay_url()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{\"resultCode\":0,\"message\":\"ok\",\"payUrl\":\"https://test-payment.momo.vn/pay/abc\"}"));
        var request = Request();

        var result = await Gateway(http).CreatePaymentAsync(Creds, request);

        result.Success.Should().BeTrue();
        result.PaymentUrl.Should().Be("https://test-payment.momo.vn/pay/abc");
        result.TransactionRef.Should().Be("MREF1");

        var (message, body) = seen.Single();
        message.RequestUri!.ToString().Should().Be("https://test-payment.momo.vn/v2/gateway/api/create");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("amount").GetInt64().Should().Be(150000);
        root.GetProperty("orderId").GetString().Should().Be("MREF1");
        root.GetProperty("requestType").GetString().Should().Be("captureWallet");

        var raw = $"accessKey={Access}&amount=150000&extraData=&ipnUrl={request.NotifyUrl}&orderId=MREF1&orderInfo=Order SO-1" +
                  $"&partnerCode={Partner}&redirectUrl={request.ReturnUrl}&requestId=MREF1&requestType=captureWallet";
        root.GetProperty("signature").GetString().Should().Be(Sha256Hex(Secret, raw));
    }

    [Fact]
    public async Task Create_refuses_a_currency_other_than_VND_without_calling_MoMo()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{}"));

        var result = await Gateway(http).CreatePaymentAsync(Creds, Request("USD"));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("VND");
        seen.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_reports_MoMo_errors_and_an_unreachable_MoMo_without_throwing()
    {
        var (declined, _) = Http((_, _) => (HttpStatusCode.OK, "{\"resultCode\":1001,\"message\":\"insufficient\"}"));
        (await Gateway(declined).CreatePaymentAsync(Creds, Request())).Error.Should().Contain("1001");

        var (broken, _) = Http((_, _) => throw new HttpRequestException("down"));
        (await Gateway(broken).CreatePaymentAsync(Creds, Request())).Error.Should().Contain("not reachable");
    }

    [Fact]
    public async Task A_production_account_needs_a_configured_production_host_and_never_uses_the_sandbox()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{}"));

        var result = await Gateway(http).CreatePaymentAsync(Creds with { Sandbox = false }, Request());

        result.Success.Should().BeFalse();
        seen.Should().BeEmpty();
    }

    // ── notification ──────────────────────────────────────────────────────

    private static string SignedBody(string orderId = "MREF1", long amount = 150000, int resultCode = 0, long transId = 4001, string? signatureOverride = null,
        string partner = Partner)
    {
        var raw = $"accessKey={Access}&amount={amount}&extraData=&message=Success&orderId={orderId}&orderInfo=Order SO-1&orderType=momo_wallet" +
                  $"&partnerCode={partner}&payType=qr&requestId={orderId}&responseTime=1700000000000&resultCode={resultCode}&transId={transId}";
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["partnerCode"] = partner, ["orderId"] = orderId, ["requestId"] = orderId, ["amount"] = amount, ["orderInfo"] = "Order SO-1",
            ["orderType"] = "momo_wallet", ["transId"] = transId, ["resultCode"] = resultCode, ["message"] = "Success", ["payType"] = "qr",
            ["responseTime"] = 1700000000000, ["extraData"] = "", ["signature"] = signatureOverride ?? Sha256Hex(Secret, raw)
        });
    }

    private static GatewayCallback Post(string body) => new("POST", new Dictionary<string, string>(), body);

    [Fact]
    public void A_correctly_signed_paid_notification_is_accepted_with_its_amount_and_transaction_id()
    {
        var verify = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory).Verify(Creds, Post(SignedBody()));

        verify.Should().BeEquivalentTo(new GatewayVerifyResult(true, true, "MREF1", "4001", null, 150000m));
    }

    [Fact]
    public void A_notification_with_a_changed_amount_fails_the_signature()
    {
        var tampered = SignedBody().Replace("\"amount\":150000", "\"amount\":1000");

        var verify = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory).Verify(Creds, Post(tampered));

        verify.Success.Should().BeFalse();
    }

    [Fact]
    public void A_wrong_or_missing_signature_or_secret_is_refused()
    {
        var gw = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);

        gw.Verify(Creds, Post(SignedBody(signatureOverride: "deadbeef"))).Success.Should().BeFalse();
        gw.Verify(Creds, Post(SignedBody(signatureOverride: ""))).Success.Should().BeFalse();
        gw.Verify(Creds with { Secret1 = "other" }, Post(SignedBody())).Success.Should().BeFalse();
        gw.Verify(Creds, Post("not json")).Success.Should().BeFalse();
    }

    [Fact]
    public void A_notification_signed_for_another_partner_is_refused_even_if_the_signature_is_valid()
    {
        var verify = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory).Verify(Creds, Post(SignedBody(partner: "SOMEONEELSE")));

        verify.Success.Should().BeFalse();
    }

    [Fact]
    public void A_signed_failure_is_verified_but_not_paid()
    {
        var verify = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory).Verify(Creds, Post(SignedBody(resultCode: 1006)));

        verify.Success.Should().BeTrue();
        verify.IsPaid.Should().BeFalse();
        verify.Error.Should().Contain("1006");
    }

    [Fact]
    public void The_browser_return_carries_the_same_signed_fields_in_the_query()
    {
        using var doc = JsonDocument.Parse(SignedBody());
        var query = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());

        var verify = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory).Verify(Creds, new GatewayCallback("GET", query, string.Empty));

        verify.Success.Should().BeTrue();
        verify.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void The_reference_can_be_read_before_verification_and_the_answer_is_204_on_success()
    {
        var gw = Gateway(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);

        gw.PeekTransactionRef(Post(SignedBody(orderId: "MREF9"))).Should().Be("MREF9");
        gw.PeekTransactionRef(Post("garbage")).Should().BeNull();
        gw.IpnResponse(true, "ok").StatusCode.Should().Be(204);
        gw.IpnResponse(false, "no").StatusCode.Should().Be(400);
    }

    // ── refund ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refund_sends_the_original_transaction_id_and_a_signature_over_the_documented_fields()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{\"resultCode\":0,\"transId\":9001,\"message\":\"ok\"}"));

        var result = await Gateway(http).RefundAsync(Creds, "4001", "MREF1", 50000m, "customer returned");

        result.Should().BeEquivalentTo(new GatewayRefundResult(true, "9001", null));
        var (message, body) = seen.Single();
        message.RequestUri!.AbsolutePath.Should().Be("/v2/gateway/api/refund");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var orderId = root.GetProperty("orderId").GetString()!;
        var requestId = root.GetProperty("requestId").GetString()!;
        root.GetProperty("transId").GetInt64().Should().Be(4001);
        var raw = $"accessKey={Access}&amount=50000&description=customer returned&orderId={orderId}&partnerCode={Partner}&requestId={requestId}&transId=4001";
        root.GetProperty("signature").GetString().Should().Be(Sha256Hex(Secret, raw));
    }

    [Fact]
    public async Task Refund_without_a_numeric_MoMo_transaction_id_or_with_a_MoMo_error_is_a_failure_not_an_exception()
    {
        var (http, _) = Http((_, _) => (HttpStatusCode.OK, "{\"resultCode\":10,\"message\":\"too late\"}"));
        var gw = Gateway(http);

        (await gw.RefundAsync(Creds, "not-a-number", "R", 1m, "x")).Success.Should().BeFalse();
        (await gw.RefundAsync(Creds, "4001", "R", 1m, "x")).Error.Should().Contain("10");
    }
}
