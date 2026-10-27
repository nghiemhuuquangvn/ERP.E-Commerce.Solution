using System.Net;
using System.Text.Json;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Infrastructure.Carriers;
using FluentAssertions;
using Xunit;
using static ERP.SHP.UnitTests.Carriers.CarrierTestSupport;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11: GHTK requests as documented on api.ghtk.vn, and its webhook.</summary>
public class GhtkCarrierProviderTests
{
    private static readonly CarrierCredentials Creds = new("GHTK", "PARTNER1", null, "API-TOKEN", "WEBHOOK-SECRET", Sandbox: true);

    private static GhtkCarrierProvider Provider(IHttpClientFactory http) => new(Config(), Log<GhtkCarrierProvider>(), http);

    [Fact]
    public async Task Quote_sends_the_weight_in_grams_with_the_two_headers_to_the_staging_host_and_reads_the_fee()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":true,\"fee\":{\"name\":\"area1\",\"fee\":30400,\"insurance_fee\":15000,\"delivery\":true}}"));

        var quote = await Provider(http).QuoteAsync(Creds, new CarrierQuoteRequest(Shop, Customer, 1200, 300_000m, 300_000m));

        quote.Should().BeEquivalentTo(new CarrierQuote("GHTK", true, 30400m, "area1", null, null));
        var request = seen.Single().Request;
        request.RequestUri!.Host.Should().Be("services-staging.ghtklab.com");
        request.RequestUri.AbsolutePath.Should().Be("/services/shipment/fee");
        request.RequestUri.Query.Should().Contain("weight=1200").And.Contain("province=Ho%20Chi%20Minh").And.Contain("transport=road");
        request.Headers.GetValues("Token").Single().Should().Be("API-TOKEN");
        request.Headers.GetValues("X-Client-Source").Single().Should().Be("PARTNER1");
    }

    [Fact]
    public async Task Quote_says_so_when_ghtk_does_not_deliver_there_or_refuses_and_a_production_account_uses_the_production_host()
    {
        var (noDelivery, _) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":true,\"fee\":{\"fee\":0,\"delivery\":false}}"));
        (await Provider(noDelivery).QuoteAsync(Creds, new CarrierQuoteRequest(Shop, Customer, 500, 0, 0))).Success.Should().BeFalse();

        var (refused, _) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":false,\"message\":\"bad province\"}"));
        (await Provider(refused).QuoteAsync(Creds, new CarrierQuoteRequest(Shop, Customer, 500, 0, 0))).Error.Should().Contain("bad province");

        var (prod, seen) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":true,\"fee\":{\"fee\":1,\"delivery\":true}}"));
        await Provider(prod).QuoteAsync(Creds with { Sandbox = false }, new CarrierQuoteRequest(Shop, Customer, 500, 0, 0));
        seen.Single().Request.RequestUri!.Host.Should().Be("services.giaohangtietkiem.vn");
    }

    [Fact]
    public async Task Book_sends_cod_as_pick_money_declared_value_grams_and_kilogram_products_and_returns_the_label()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK,
            "{\"success\":true,\"order\":{\"partner_id\":\"SO-1001\",\"label\":\"S1.A1.17373471\",\"fee\":\"32000\",\"tracking_id\":99,\"estimated_deliver_time\":\"Sáng 2026-10-30\"}}"));

        var booking = await Provider(http).BookAsync(Creds, Book());

        booking.Success.Should().BeTrue();
        booking.TrackingCode.Should().Be("S1.A1.17373471");
        booking.Fee.Should().Be(32000m);
        booking.EstimatedDelivery.Should().Be(new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc));

        var (request, body) = seen.Single();
        request.RequestUri!.PathAndQuery.Should().Be("/services/shipment/order?ver=1.5");
        using var json = JsonDocument.Parse(body);
        var order = json.RootElement.GetProperty("order");
        order.GetProperty("id").GetString().Should().Be("SO-1001");
        order.GetProperty("pick_money").GetInt64().Should().Be(300000);
        order.GetProperty("value").GetInt64().Should().Be(300000);
        order.GetProperty("is_freeship").GetInt32().Should().Be(1, "the shop pays GHTK; the recipient owes only the COD amount");
        order.GetProperty("weight_option").GetString().Should().Be("gram");
        order.GetProperty("total_weight").GetInt32().Should().Be(1200);
        order.GetProperty("ward").GetString().Should().Be("Phuong 1");
        json.RootElement.GetProperty("products")[0].GetProperty("weight").GetDecimal().Should().Be(0.6m);
    }

    [Fact]
    public async Task Book_of_a_prepaid_order_collects_nothing_and_a_refused_or_unreachable_ghtk_is_an_error_not_a_success()
    {
        var (http, seen) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":true,\"order\":{\"label\":\"X\"}}"));
        await Provider(http).BookAsync(Creds, Book(cod: 0));
        JsonDocument.Parse(seen.Single().Body).RootElement.GetProperty("order").GetProperty("pick_money").GetInt64().Should().Be(0);

        var (refused, _) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":false,\"message\":\"Phone invalid\"}"));
        (await Provider(refused).BookAsync(Creds, Book())).Error.Should().Contain("Phone invalid");

        var (broken, _) = Http((_, _) => throw new HttpRequestException("down"));
        var down = await Provider(broken).BookAsync(Creds, Book());
        down.Success.Should().BeFalse();
        down.Error.Should().Contain("not reachable");

        var (noLabel, _) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":true,\"order\":{}}"));
        (await Provider(noLabel).BookAsync(Creds, Book())).Success.Should().BeFalse();
    }

    [Fact]
    public async Task Booking_the_same_order_twice_returns_the_existing_parcel()
    {
        var (http, _) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":false,\"error\":{\"code\":\"ORDER_ID_EXIST\",\"ghtk_label\":\"S1.OLD\"}}"));

        var booking = await Provider(http).BookAsync(Creds, Book());

        booking.Success.Should().BeTrue();
        booking.TrackingCode.Should().Be("S1.OLD");
    }

    [Fact]
    public async Task Cancel_and_status_use_the_label_in_the_path()
    {
        var (http, seen) = Http((r, _) => r.Method == HttpMethod.Post
            ? (HttpStatusCode.OK, "{\"success\":true,\"message\":\"\",\"log_id\":\"1\"}")
            : (HttpStatusCode.OK, "{\"success\":true,\"order\":{\"status\":\"5\",\"status_text\":\"Da giao hang\",\"modified\":\"2026-10-29 10:00:00\"}}"));
        var provider = Provider(http);

        (await provider.CancelAsync(Creds, "S1.A1.1", "customer cancelled")).Success.Should().BeTrue();
        var status = await provider.GetStatusAsync(Creds, "S1.A1.1");

        status.Status.Should().Be(CarrierShipmentStatus.Delivered);
        seen[0].Request.RequestUri!.AbsolutePath.Should().Be("/services/shipment/cancel/S1.A1.1");
        seen[1].Request.RequestUri!.AbsolutePath.Should().Be("/services/shipment/v2/S1.A1.1");

        var (refused, _) = Http((_, _) => (HttpStatusCode.OK, "{\"success\":false,\"message\":\"already picked up\"}"));
        (await Provider(refused).CancelAsync(Creds, "S1", "x")).Error.Should().Contain("already picked up");
    }

    [Theory]
    [InlineData("-1", CarrierShipmentStatus.Cancelled)]
    [InlineData("1", CarrierShipmentStatus.Accepted)]
    [InlineData("2", CarrierShipmentStatus.Accepted)]
    [InlineData("3", CarrierShipmentStatus.PickedUp)]
    [InlineData("4", CarrierShipmentStatus.OutForDelivery)]
    [InlineData("5", CarrierShipmentStatus.Delivered)]
    [InlineData("6", CarrierShipmentStatus.Delivered)]
    [InlineData("9", CarrierShipmentStatus.DeliveryFailed)]
    [InlineData("20", CarrierShipmentStatus.Returning)]
    [InlineData("21", CarrierShipmentStatus.Returned)]
    [InlineData("45", CarrierShipmentStatus.Accepted)]   // reported by the shipper: not official, never delivers an order
    [InlineData("49", CarrierShipmentStatus.Accepted)]
    [InlineData("999", CarrierShipmentStatus.Accepted)]
    public void Status_ids_map_as_in_the_docs_and_shipper_reports_never_change_the_state(string id, CarrierShipmentStatus expected) =>
        GhtkCarrierProvider.MapStatus(id).Should().Be(expected);

    private const string Json = "{\"partner_id\":\"SO-1001\",\"label_id\":\"S1.A1.17373471\",\"status_id\":5,\"action_time\":\"2026-10-29T10:15:00+07:00\",\"reason\":\"ok\",\"pick_money\":300000,\"fee\":15000}";

    [Fact]
    public void A_notification_with_the_shops_secret_is_verified_and_translated()
    {
        var provider = Provider(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);

        provider.PeekTrackingCode(Post(Json)).Should().Be("S1.A1.17373471");
        var e = provider.VerifyWebhook(Creds, Post(Json, hash: "WEBHOOK-SECRET"));

        e.Verified.Should().BeTrue();
        e.TrackingCode.Should().Be("S1.A1.17373471");
        e.Status.Should().Be(CarrierShipmentStatus.Delivered);
        e.CarrierStatus.Should().Be("5");
        e.CodAmount.Should().Be(300000m);
        e.ActionTime.Should().Be(new DateTime(2026, 10, 29, 3, 15, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_form_encoded_notification_works_too_and_a_wrong_or_missing_secret_is_refused()
    {
        var provider = Provider(Http((_, _) => (HttpStatusCode.OK, "{}")).Factory);
        var form = "partner_id=SO-1&label_id=S1.X&status_id=4&action_time=2026-10-29T10%3A00%3A00%2B07%3A00&reason=";

        provider.VerifyWebhook(Creds, Post(form, "WEBHOOK-SECRET")).Status.Should().Be(CarrierShipmentStatus.OutForDelivery);
        provider.VerifyWebhook(Creds, Post(Json, "nope")).Verified.Should().BeFalse();
        provider.VerifyWebhook(Creds, Post(Json)).Verified.Should().BeFalse();
        provider.VerifyWebhook(Creds with { WebhookSecret = null }, Post(Json, "")).Verified.Should().BeFalse("a shop that set no secret accepts nothing");
        provider.VerifyWebhook(Creds, Post("not a notification", "WEBHOOK-SECRET")).Verified.Should().BeFalse();
        provider.WebhookReply(true, "ok").StatusCode.Should().Be(200);
    }
}
