using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11: selection by rule, then by price, with failover - and a booking failure that is never swallowed.</summary>
public class CarrierBookingServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly CarrierAddress Shop = new("Shop", "0900000001", "1 Nguyen Hue", null, "Quan 1", "Ho Chi Minh");

    private readonly Mock<IShippingUnitOfWork> _uow = new();
    private readonly Mock<IShippingConfigRepository> _config = new();
    private readonly Mock<ICarrierRepository> _carriers = new();
    private readonly Mock<ICarrierProviderResolver> _resolver = new();
    private readonly Dictionary<string, FakeCarrier> _fakes = new();
    private readonly List<CarrierSelectionRule> _rules = new();

    private sealed class FakeCarrier : ICarrierProvider
    {
        public string Code { get; init; } = string.Empty;
        public decimal? Fee { get; init; }
        public string? BookError { get; init; }
        public int Books { get; private set; }

        public Task<CarrierQuote> QuoteAsync(CarrierCredentials c, CarrierQuoteRequest r, CancellationToken ct = default) =>
            Task.FromResult(Fee is { } f ? new CarrierQuote(Code, true, f, null, null, null) : new CarrierQuote(Code, false, 0, null, null, "no quote"));

        public Task<CarrierBooking> BookAsync(CarrierCredentials c, CarrierBookRequest r, CancellationToken ct = default)
        {
            Books++;
            return Task.FromResult(BookError == null ? new CarrierBooking(true, Code + "-TRACK", Code + "-ORD", Fee, null, null) : new CarrierBooking(false, null, null, null, null, BookError));
        }

        public Task<CarrierCancelResult> CancelAsync(CarrierCredentials c, string t, string reason, CancellationToken ct = default) => Task.FromResult(new CarrierCancelResult(true, null));
        public Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials c, string t, CancellationToken ct = default) => throw new NotSupportedException();
        public string? PeekTrackingCode(CarrierWebhookRequest r) => null;
        public CarrierWebhookEvent VerifyWebhook(CarrierCredentials c, CarrierWebhookRequest r) => throw new NotSupportedException();
        public CarrierWebhookReply WebhookReply(bool accepted, string message) => new(200, null);
    }

    public CarrierBookingServiceTests()
    {
        _uow.SetupGet(u => u.Config).Returns(_config.Object);
        _uow.SetupGet(u => u.Carriers).Returns(_carriers.Object);
        _config.Setup(c => c.GetActiveRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _rules);
        var settings = TenantShippingSettings.CreateDefault(Tenant);
        settings.UpdateSender("Shop", "0900000001", "1 Nguyen Hue", null, "Quan 1", "Ho Chi Minh", 500);
        _config.Setup(c => c.GetTenantSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _carriers.Setup(c => c.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Carrier?)null);
        _resolver.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fakes.Values.Select(f => new ResolvedCarrier(f, new CarrierCredentials(f.Code, "x", null, "s", null, true))).ToList());
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new ResolvedCarrier(_fakes[code], new CarrierCredentials(code, "x", null, "s", null, true)));
    }

    private void Carrier(string code, decimal? fee, string? bookError = null) => _fakes[code] = new FakeCarrier { Code = code, Fee = fee, BookError = bookError };

    private CarrierBookingService Service() => new(_uow.Object, _resolver.Object, NullLogger<CarrierBookingService>.Instance);

    private static Shipment NewShipment(decimal cod = 300_000m, string province = "Ho Chi Minh")
    {
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.Empty, "(booking)",
            new ShipmentRecipient("An", "0911111111", "2 Le Loi", null, "Quan 3", province, null),
            new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Shirt", 2, 0.6m) });
        shipment.SetCommercials(cod, 300_000m);
        return shipment;
    }

    private Task Book(Shipment shipment, string? only = null) =>
        Service().BookAsync(shipment, Shop, new List<CarrierParcelItem> { new("Shirt", 2, 150_000m, 600) }, new BookingOrder("SO-1", null), only);

    [Fact]
    public async Task With_no_rule_the_cheapest_quote_wins_and_the_shipment_records_why()
    {
        Carrier("GHTK", 32000m);
        Carrier("VTP", 27500m);
        var shipment = NewShipment();

        await Book(shipment);

        shipment.TrackingNumber.Should().Be("VTP-TRACK");
        shipment.CarrierFee.Should().Be(27500m);
        shipment.SelectionNote.Should().Contain("cheapest").And.Contain("27");
        shipment.CarrierName.Should().Be("Viettel Post");
        _fakes["GHTK"].Books.Should().Be(0, "the first acceptable carrier ends the search");
    }

    [Fact]
    public async Task A_matching_rule_beats_a_cheaper_quote_and_priority_decides_between_rules()
    {
        Carrier("GHTK", 32000m);
        Carrier("VTP", 20000m);
        var low = CarrierSelectionRule.Create(Tenant, "VTP everywhere", 50, "VTP");
        var high = CarrierSelectionRule.Create(Tenant, "COD to HCM by GHTK", 10, "GHTK");
        high.Update("COD to HCM by GHTK", 10, true, "Hồ Chí Minh", null, null, null, null, true, "GHTK");
        _rules.AddRange(new[] { low, high });
        var shipment = NewShipment();

        await Book(shipment);

        shipment.TrackingNumber.Should().Be("GHTK-TRACK");
        shipment.SelectionNote.Should().Contain("rule 'COD to HCM by GHTK'");
    }

    [Fact]
    public async Task A_rule_that_does_not_match_the_order_or_names_an_unusable_carrier_is_skipped()
    {
        Carrier("GHTK", 32000m);
        Carrier("VTP", 20000m);
        var other = CarrierSelectionRule.Create(Tenant, "Hanoi only", 1, "GHTK");
        other.Update("Hanoi only", 1, true, "Hà Nội", null, null, null, null, null, "GHTK");
        _rules.Add(other);
        _rules.Add(CarrierSelectionRule.Create(Tenant, "GHN for everything", 2, "GHN"));   // GHN is not usable for this shop

        var shipment = NewShipment();
        await Book(shipment);

        shipment.TrackingNumber.Should().Be("VTP-TRACK");
    }

    [Fact]
    public async Task A_refusing_carrier_hands_the_parcel_to_the_next_one_and_the_note_keeps_the_refusal()
    {
        Carrier("GHTK", 20000m, bookError: "GHTK refused the order: Phone invalid");
        Carrier("VTP", 27500m);
        var shipment = NewShipment();

        await Book(shipment);

        shipment.TrackingNumber.Should().Be("VTP-TRACK");
        shipment.SelectionNote.Should().Contain("after: GHTK: GHTK refused the order: Phone invalid");
    }

    [Fact]
    public async Task A_carrier_that_cannot_quote_is_tried_after_the_priced_ones_not_never()
    {
        Carrier("GHN", null);                  // cannot quote (needs ids)
        Carrier("GHTK", 32000m, bookError: "no coverage");
        var shipment = NewShipment();

        await Book(shipment);

        shipment.TrackingNumber.Should().Be("GHN-TRACK");
        _fakes["GHTK"].Books.Should().Be(1);
    }

    [Fact]
    public async Task When_every_carrier_refuses_the_failure_lists_them_all_and_nothing_is_swallowed()
    {
        Carrier("GHTK", 20000m, bookError: "address not covered");
        Carrier("VTP", 27500m, bookError: "Viettel Post does not know the district");
        var shipment = NewShipment();

        var act = () => Book(shipment);

        var ex = (await act.Should().ThrowAsync<CarrierBookingException>()).Which;
        ex.Message.Should().Contain("GHTK: address not covered").And.Contain("VTP: Viettel Post does not know the district");
        ex.IsTransient.Should().BeFalse();
        shipment.BookingError.Should().Contain("GHTK");
    }

    [Fact]
    public async Task When_every_failure_is_an_unreachable_carrier_the_failure_is_transient()
    {
        Carrier("GHTK", 20000m, bookError: "the carrier is not reachable: timeout");

        var act = () => Book(NewShipment());

        (await act.Should().ThrowAsync<CarrierBookingException>()).Which.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task A_shop_with_no_usable_carrier_or_no_pickup_address_gets_a_clear_reason()
    {
        var none = () => Book(NewShipment());
        (await none.Should().ThrowAsync<CarrierBookingException>()).WithMessage("*no carrier is set up*");

        _config.Setup(c => c.GetTenantSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TenantShippingSettings.CreateDefault(Tenant));
        var noSender = () => Service().SenderAsync();
        (await noSender.Should().ThrowAsync<CarrierBookingException>()).WithMessage("*pickup address*");
    }

    [Fact]
    public async Task A_carrier_chosen_by_a_person_is_the_only_one_tried()
    {
        Carrier("GHTK", 32000m);
        Carrier("VTP", 20000m);
        var shipment = NewShipment();

        await Book(shipment, only: "GHTK");

        shipment.TrackingNumber.Should().Be("GHTK-TRACK");
        shipment.SelectionNote.Should().Contain("chosen by a person");
        _fakes["VTP"].Books.Should().Be(0);
    }

    [Fact]
    public async Task The_shops_carrier_row_is_created_on_first_use_and_the_shipment_points_at_it()
    {
        Carrier("GHTK", 32000m);
        var shipment = NewShipment();

        await Book(shipment);

        _carriers.Verify(c => c.AddAsync(It.Is<Carrier>(x => x.Code == "GHTK"), It.IsAny<CancellationToken>()), Times.Once);
        shipment.CarrierId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task The_parcel_sent_to_the_carrier_carries_the_weight_in_grams_the_cod_amount_and_the_declared_value()
    {
        CarrierBookRequest? seen = null;
        var provider = new Mock<ICarrierProvider>();
        provider.SetupGet(p => p.Code).Returns("GHTK");
        provider.Setup(p => p.QuoteAsync(It.IsAny<CarrierCredentials>(), It.IsAny<CarrierQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CarrierQuote("GHTK", true, 1, null, null, null));
        provider.Setup(p => p.BookAsync(It.IsAny<CarrierCredentials>(), It.IsAny<CarrierBookRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CarrierCredentials, CarrierBookRequest, CancellationToken>((_, r, _) => seen = r)
            .ReturnsAsync(new CarrierBooking(true, "T", null, 1, null, null));
        _resolver.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResolvedCarrier> { new(provider.Object, new CarrierCredentials("GHTK", "x", null, "s", null, true)) });

        await Book(NewShipment());

        seen!.WeightGrams.Should().Be(1200);
        seen.CodAmount.Should().Be(300_000m);
        seen.DeclaredValue.Should().Be(300_000m);
        seen.OrderReference.Should().Be("SO-1");
        seen.To.District.Should().Be("Quan 3");
        seen.From.Name.Should().Be("Shop");
    }
}
