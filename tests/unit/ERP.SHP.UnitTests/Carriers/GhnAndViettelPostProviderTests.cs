using System.Net;
using System.Text.Json;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Infrastructure.Carriers;
using FluentAssertions;
using Xunit;
using static ERP.SHP.UnitTests.Carriers.CarrierTestSupport;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11: GHN (documented on developer.ghn.vn) and Viettel Post (NOT verified against its partner documentation - see the provider) and the J&amp;T placeholder.</summary>
public class GhnAndViettelPostProviderTests
{
    // ── GHN ───────────────────────────────────────────────────────────────

    private static readonly CarrierCredentials Ghn = new("GHN", "92837", null, "GHN-TOKEN", "WH", Sandbox: true);
    private static GhnCarrierProvider GhnProvider(IHttpClientFactory http) => new(Config(), Log<GhnCarrierProvider>(), http);

    [Fact]
    public async Task Ghn_books_with_place_names_cod_amount_shop_pays_fee_and_a_client_order_code()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK,
            "{\"code\":200,\"message\":\"Success\",\"data\":{\"order_code\":\"LADFYR\",\"total_fee\":20900,\"expected_delivery_time\":\"2026-10-30T16:59:59Z\"}}"));

        var booking = await GhnProvider(http).BookAsync(Ghn, Book());

        booking.Should().BeEquivalentTo(new CarrierBooking(true, "LADFYR", "LADFYR", 20900m, new DateTime(2026, 10, 30, 16, 59, 59, DateTimeKind.Utc), null));
        var (request, body) = seen.Single();
        request.RequestUri!.ToString().Should().Be("https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/create");
        request.Headers.GetValues("ShopId").Single().Should().Be("92837");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("cod_amount").GetInt64().Should().Be(300000, "the old client always sent 0");
        root.GetProperty("payment_type_id").GetInt32().Should().Be(1);
        root.GetProperty("client_order_code").GetString().Should().Be("SO-1001");
        root.GetProperty("to_district_name").GetString().Should().Be("Quan 3");
        root.GetProperty("weight").GetInt32().Should().Be(1200);
    }

    [Fact]
    public async Task Ghn_refuses_more_cod_than_it_collects_caps_insurance_and_reports_errors()
    {
        var (never, seen) = Http((_, _) => (HttpStatusCode.OK, "{}"));
        (await GhnProvider(never).BookAsync(Ghn, Book(cod: 60_000_000m))).Error.Should().Contain("50,000,000");
        seen.Should().BeEmpty();

        var (http, sent) = Http((_, _) => (HttpStatusCode.OK, "{\"code\":400,\"message\":\"PHONE_INVALID\"}"));
        var big = Book() with { DeclaredValue = 9_000_000m };
        (await GhnProvider(http).BookAsync(Ghn, big)).Error.Should().Contain("PHONE_INVALID");
        JsonDocument.Parse(sent.Single().Body).RootElement.GetProperty("insurance_value").GetInt64().Should().Be(5_000_000);
    }

    [Fact]
    public async Task Ghn_cannot_quote_without_district_ids_and_cancel_checks_the_per_order_result()
    {
        var provider = GhnProvider(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);
        (await provider.QuoteAsync(Ghn, new CarrierQuoteRequest(Shop, Customer, 500, 0, 0))).Success.Should().BeFalse();

        var (ok, _) = Http((_, _) => (HttpStatusCode.OK, "{\"code\":200,\"data\":[{\"order_code\":\"A\",\"result\":true,\"message\":\"OK\"}]}"));
        (await GhnProvider(ok).CancelAsync(Ghn, "A", "x")).Success.Should().BeTrue();

        var (no, _) = Http((_, _) => (HttpStatusCode.OK, "{\"code\":200,\"data\":[{\"order_code\":\"A\",\"result\":false,\"message\":\"too late\"}]}"));
        (await GhnProvider(no).CancelAsync(Ghn, "A", "x")).Error.Should().Contain("too late");
    }

    [Theory]
    [InlineData("ready_to_pick", CarrierShipmentStatus.Accepted)]
    [InlineData("picking", CarrierShipmentStatus.Accepted)]
    [InlineData("storing", CarrierShipmentStatus.InTransit)]
    [InlineData("delivering", CarrierShipmentStatus.OutForDelivery)]
    [InlineData("delivered", CarrierShipmentStatus.Delivered)]
    [InlineData("delivery_fail", CarrierShipmentStatus.DeliveryFailed)]
    [InlineData("return", CarrierShipmentStatus.Returning)]
    [InlineData("returned", CarrierShipmentStatus.Returned)]
    [InlineData("cancel", CarrierShipmentStatus.Cancelled)]
    [InlineData("lost", CarrierShipmentStatus.Accepted)]
    public void Ghn_statuses_map(string status, CarrierShipmentStatus expected) => GhnCarrierProvider.MapStatus(status).Should().Be(expected);

    [Fact]
    public void Ghn_webhook_needs_the_shops_secret_and_carries_the_cod_amount()
    {
        var provider = GhnProvider(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);
        var body = "{\"OrderCode\":\"LADFYR\",\"Status\":\"delivered\",\"Time\":\"2026-10-29T03:15:00Z\",\"CODAmount\":300000,\"Type\":\"switch_status\"}";

        provider.PeekTrackingCode(Post(body)).Should().Be("LADFYR");
        var e = provider.VerifyWebhook(Ghn, Post(body, "WH"));
        e.Verified.Should().BeTrue();
        e.Status.Should().Be(CarrierShipmentStatus.Delivered);
        e.CodAmount.Should().Be(300000m);
        provider.VerifyWebhook(Ghn, Post(body, "bad")).Verified.Should().BeFalse();
    }

    // ── Viettel Post ──────────────────────────────────────────────────────

    private static readonly CarrierCredentials Vtp = new("VTP", null, "shop@x.test", "pw", "VTP-SECRET", Sandbox: true);
    private static ViettelPostCarrierProvider VtpProvider(IHttpClientFactory http) => new(Config(), Log<ViettelPostCarrierProvider>(), http);

    private static (HttpStatusCode, string) VtpApi(HttpRequestMessage r, string body)
    {
        var path = r.RequestUri!.AbsolutePath;
        if (path.EndsWith("/user/Login")) return (HttpStatusCode.OK, "{\"status\":200,\"data\":{\"token\":\"TOK\"}}");
        if (path.EndsWith("/listProvinceById")) return (HttpStatusCode.OK, "{\"data\":[{\"PROVINCE_ID\":2,\"PROVINCE_NAME\":\"Hồ Chí Minh\"},{\"PROVINCE_ID\":1,\"PROVINCE_NAME\":\"Hà Nội\"}]}");
        if (path.EndsWith("/listDistrict")) return (HttpStatusCode.OK, "{\"data\":[{\"DISTRICT_ID\":20,\"DISTRICT_NAME\":\"Quận 1\"},{\"DISTRICT_ID\":23,\"DISTRICT_NAME\":\"Quận 3\"}]}");
        if (path.EndsWith("/listWards")) return (HttpStatusCode.OK, "{\"data\":[{\"WARDS_ID\":201,\"WARDS_NAME\":\"Phường Bến Nghé\"},{\"WARDS_ID\":231,\"WARDS_NAME\":\"Phường 1\"}]}");
        if (path.EndsWith("/order/getPrice")) return (HttpStatusCode.OK, "{\"error\":false,\"data\":{\"MONEY_TOTAL\":27500}}");
        if (path.EndsWith("/order/createOrder")) return (HttpStatusCode.OK, "{\"error\":false,\"data\":{\"ORDER_NUMBER\":\"VTP778\",\"MONEY_TOTAL\":27500}}");
        return (HttpStatusCode.NotFound, "{}");
    }

    [Fact]
    public async Task Viettel_post_logs_in_finds_places_by_name_and_books_with_ids_and_the_cod_amount()
    {
        var (http, seen) = Http(VtpApi);

        var booking = await VtpProvider(http).BookAsync(Vtp, Book());

        booking.Success.Should().BeTrue();
        booking.TrackingCode.Should().Be("VTP778");
        var create = seen.Single(s => s.Request.RequestUri!.AbsolutePath.EndsWith("/order/createOrder"));
        create.Request.Headers.GetValues("Token").Single().Should().Be("TOK");
        create.Request.RequestUri!.Host.Should().Be("partnerdev.viettelpost.vn");
        using var json = JsonDocument.Parse(create.Body);
        var root = json.RootElement;
        root.GetProperty("RECEIVER_PROVINCE").GetInt64().Should().Be(2);
        root.GetProperty("RECEIVER_DISTRICT").GetInt64().Should().Be(23);
        root.GetProperty("RECEIVER_WARD").GetInt64().Should().Be(231);
        root.GetProperty("MONEY_COLLECTION").GetInt64().Should().Be(300000);
        root.GetProperty("ORDER_PAYMENT").GetInt32().Should().Be(3);
        root.GetProperty("PRODUCT_WEIGHT").GetInt32().Should().Be(1200);
    }

    [Fact]
    public async Task Viettel_post_quotes_and_reports_an_address_it_cannot_match_instead_of_guessing()
    {
        var (http, _) = Http(VtpApi);
        var quote = await VtpProvider(http).QuoteAsync(Vtp, new CarrierQuoteRequest(Shop, Customer, 1200, 300_000m, 0));
        quote.Success.Should().BeTrue();
        quote.Fee.Should().Be(27500m);

        var (http2, _) = Http(VtpApi);
        var far = Customer with { Province = "Atlantis" };
        var booking = await VtpProvider(http2).BookAsync(Vtp, Book() with { To = far });
        booking.Success.Should().BeFalse();
        booking.Error.Should().Contain("Atlantis");
    }

    [Fact]
    public async Task Viettel_post_refusals_and_login_failures_are_errors()
    {
        var (refuse, _) = Http((r, b) => r.RequestUri!.AbsolutePath.EndsWith("/order/createOrder")
            ? (HttpStatusCode.OK, "{\"error\":true,\"message\":\"Sai so dien thoai\"}") : VtpApi(r, b));
        (await VtpProvider(refuse).BookAsync(Vtp, Book())).Error.Should().Contain("Sai so dien thoai");

        var (badLogin, _) = Http((_, _) => (HttpStatusCode.OK, "{\"status\":400,\"message\":\"wrong password\"}"));
        var result = await VtpProvider(badLogin).BookAsync(Vtp with { Username = "someone-else@x.test" }, Book());
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("wrong password");
    }

    [Theory]
    [InlineData("Giao hàng thành công", CarrierShipmentStatus.Delivered)]
    [InlineData("Giao hàng không thành công", CarrierShipmentStatus.DeliveryFailed)]
    [InlineData("Đang giao hàng", CarrierShipmentStatus.OutForDelivery)]
    [InlineData("Đã trả hàng cho người gửi", CarrierShipmentStatus.Returned)]
    [InlineData("Chuyển hoàn", CarrierShipmentStatus.Returning)]
    [InlineData("Đã hủy", CarrierShipmentStatus.Cancelled)]
    [InlineData("Đang vận chuyển", CarrierShipmentStatus.InTransit)]
    [InlineData("Đã lấy hàng", CarrierShipmentStatus.PickedUp)]
    [InlineData("Chờ duyệt", CarrierShipmentStatus.Accepted)]
    [InlineData("", CarrierShipmentStatus.Accepted)]
    public void Viettel_post_statuses_are_read_from_their_names_and_only_an_explicit_success_delivers(string name, CarrierShipmentStatus expected) =>
        ViettelPostCarrierProvider.MapStatus("500", name).Should().Be(expected);

    [Fact]
    public void Viettel_post_webhook_is_verified_by_the_token_in_the_payload_or_the_callback_address()
    {
        var provider = VtpProvider(Http(VtpApi).Factory);
        string Body(string token) => "{\"DATA\":{\"ORDER_NUMBER\":\"VTP778\",\"ORDER_STATUS\":501,\"ORDER_STATUSDATE\":\"29/10/2026 10:15:00\",\"STATUS_NAME\":\"Giao hàng thành công\",\"MONEY_COLLECTION\":300000},\"TOKEN\":\"" + token + "\"}";

        provider.PeekTrackingCode(Post(Body("x"))).Should().Be("VTP778");
        var e = provider.VerifyWebhook(Vtp, Post(Body("VTP-SECRET")));
        e.Verified.Should().BeTrue();
        e.Status.Should().Be(CarrierShipmentStatus.Delivered);
        e.CodAmount.Should().Be(300000m);
        provider.VerifyWebhook(Vtp, Post(Body("wrong"), hash: "VTP-SECRET")).Verified.Should().BeTrue();
        provider.VerifyWebhook(Vtp, Post(Body("wrong"))).Verified.Should().BeFalse();
    }

    // ── J&T ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task JAndT_is_a_placeholder_that_says_so_and_never_books()
    {
        var jnt = new JAndTCarrierProvider();

        jnt.Implemented.Should().BeFalse();
        (await jnt.BookAsync(new CarrierCredentials("JNT", null, null, null, null, true), Book())).Error.Should().Contain("not integrated");
        jnt.VerifyWebhook(new CarrierCredentials("JNT", null, null, null, null, true), Post("{}")).Verified.Should().BeFalse();
    }
}
