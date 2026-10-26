using System.Net;
using System.Web;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Infrastructure.Gateway;
using FluentAssertions;
using Xunit;
using static ERP.PAY.UnitTests.Gateways.GatewayTestSupport;

namespace ERP.PAY.UnitTests.Gateways;

/// <summary>VNPay after the move to per-tenant credentials: same signing as before, now with the amount in the verified result.</summary>
public class VnPayGatewayTests
{
    private static readonly GatewayCredentials Creds = new("VNPay", "TMN01", null, "HASHSECRET", null, true);
    private static readonly DateTime Now = new(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc);

    private static VnPayGateway Gateway() => new(Config(), Log<VnPayGateway>(), Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);

    [Fact]
    public async Task A_created_url_verifies_with_the_same_secret_and_carries_the_amount_in_cents()
    {
        var gw = Gateway();
        var created = await gw.CreatePaymentAsync(Creds, new GatewayPaymentRequest(Guid.NewGuid(), 150000m, "VND", "REF123", "Order 1", "https://shop.test/cb", "https://shop.test/ipn", Now));

        created.Success.Should().BeTrue();
        var query = HttpUtility.ParseQueryString(new Uri(created.PaymentUrl).Query).AllKeys.ToDictionary(k => k!, k => HttpUtility.ParseQueryString(new Uri(created.PaymentUrl).Query)[k]!);
        query["vnp_Amount"].Should().Be("15000000");

        // What VNPay would send back: the same fields plus a response code, re-signed.
        query.Remove("vnp_SecureHash");
        query["vnp_ResponseCode"] = "00";
        query["vnp_TransactionNo"] = "TX9";
        var raw = string.Join("&", query.OrderBy(k => k.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        query["vnp_SecureHash"] = Sha512Hex(Creds.Secret1!, raw);

        var verify = gw.Verify(Creds, new GatewayCallback("GET", query, string.Empty));

        verify.Should().BeEquivalentTo(new GatewayVerifyResult(true, true, "REF123", "TX9", null, 150000m));
    }

    [Fact]
    public void A_changed_field_or_a_missing_hash_or_the_wrong_secret_is_refused()
    {
        var gw = Gateway();
        var query = new Dictionary<string, string> { ["vnp_TxnRef"] = "R", ["vnp_Amount"] = "100", ["vnp_ResponseCode"] = "00" };
        var raw = "vnp_Amount=100&vnp_ResponseCode=00&vnp_TxnRef=R";
        query["vnp_SecureHash"] = Sha512Hex(Creds.Secret1!, raw);

        gw.Verify(Creds, new GatewayCallback("GET", query, "")).Success.Should().BeTrue();
        gw.Verify(Creds, new GatewayCallback("GET", new Dictionary<string, string>(query) { ["vnp_Amount"] = "1" }, "")).Success.Should().BeFalse();
        gw.Verify(Creds, new GatewayCallback("GET", query.Where(k => k.Key != "vnp_SecureHash").ToDictionary(k => k.Key, k => k.Value), "")).Success.Should().BeFalse();
        gw.Verify(Creds with { Secret1 = "other" }, new GatewayCallback("GET", query, "")).Success.Should().BeFalse();
    }

    [Fact]
    public async Task Only_VND_is_accepted_and_two_attempts_get_two_references()
    {
        var gw = Gateway();
        var id = Guid.NewGuid();

        (await gw.CreatePaymentAsync(Creds, new GatewayPaymentRequest(id, 10m, "USD", "R", "o", "u", "n", Now))).Success.Should().BeFalse();
        gw.NewTransactionRef(id, Now).Should().NotBe(gw.NewTransactionRef(id, Now.AddSeconds(30)));
        gw.IpnResponse(true, "ok").Body.Should().BeEquivalentTo(new { RspCode = "00", Message = "ok" });
    }
}
