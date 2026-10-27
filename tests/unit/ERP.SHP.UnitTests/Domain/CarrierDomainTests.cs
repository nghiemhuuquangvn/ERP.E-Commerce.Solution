using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Domain.Text;
using FluentAssertions;
using Xunit;

namespace ERP.SHP.UnitTests.Domain;

/// <summary>ROADMAP P1-11: province matching, selection rules and the carrier-status state machine.</summary>
public class CarrierDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Theory]
    [InlineData("TP. Hồ Chí Minh", "ho chi minh")]
    [InlineData("Thành phố Hồ Chí Minh", "ho chi minh")]
    [InlineData("  Tỉnh Đồng Nai ", "dong nai")]
    [InlineData("Quận 1", "1")]
    [InlineData("Hà Nội", "ha noi")]
    public void Place_names_are_compared_without_case_diacritics_or_administrative_prefixes(string written, string normalized)
    {
        VnText.Normalize(written).Should().Be(normalized);
    }

    [Fact]
    public void Same_province_written_two_ways_matches_and_different_ones_do_not()
    {
        VnText.Same("TP.HCM", "Ho Chi Minh").Should().BeFalse("an abbreviation is not guessed");
        VnText.Same("Thành phố Hồ Chí Minh", "TP. Hồ Chí Minh").Should().BeTrue();
        VnText.Same("Hà Nội", "Hải Phòng").Should().BeFalse();
        VnText.Same("", "").Should().BeFalse();
    }

    private static CarrierSelectionRule Rule(Action<CarrierSelectionRule>? configure = null)
    {
        var rule = CarrierSelectionRule.Create(Tenant, "Test", 10, "ghtk");
        configure?.Invoke(rule);
        return rule;
    }

    private static readonly ShipmentFacts Order = new("TP. Hồ Chí Minh", 1500, 800_000m, IsCashOnDelivery: true);

    [Fact]
    public void A_rule_with_no_conditions_matches_everything_and_names_its_carrier_in_canonical_case()
    {
        var rule = Rule();

        rule.CarrierCode.Should().Be("GHTK");
        rule.Matches(Order).Should().BeTrue();
    }

    [Fact]
    public void Every_condition_must_hold()
    {
        Rule(r => r.Update("t", 1, true, "Hà Nội, Hải Phòng", null, null, null, null, null, "GHTK")).Matches(Order).Should().BeFalse();
        Rule(r => r.Update("t", 1, true, "Hà Nội, hồ chí minh", null, null, null, null, null, "GHTK")).Matches(Order).Should().BeTrue();
        Rule(r => r.Update("t", 1, true, null, 2000, null, null, null, null, "GHTK")).Matches(Order).Should().BeFalse("1500 g is under the minimum");
        Rule(r => r.Update("t", 1, true, null, null, 1500, null, null, null, "GHTK")).Matches(Order).Should().BeTrue("the maximum is inclusive");
        Rule(r => r.Update("t", 1, true, null, null, null, 1_000_000m, null, null, "GHTK")).Matches(Order).Should().BeFalse();
        Rule(r => r.Update("t", 1, true, null, null, null, null, 500_000m, null, "GHTK")).Matches(Order).Should().BeFalse();
        Rule(r => r.Update("t", 1, true, null, null, null, null, null, false, "GHTK")).Matches(Order).Should().BeFalse("prepaid only");
        Rule(r => r.Update("t", 1, true, null, null, null, null, null, true, "GHTK")).Matches(Order).Should().BeTrue("COD only");
        Rule(r => r.Update("t", 1, false, null, null, null, null, null, null, "GHTK")).Matches(Order).Should().BeFalse("an inactive rule never matches");
    }

    [Fact]
    public void A_rule_refuses_an_unknown_carrier_and_contradictory_limits()
    {
        var rule = Rule();

        FluentActions.Invoking(() => rule.Update("t", 1, true, null, null, null, null, null, null, "FEDEX")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => rule.Update("t", 1, true, null, 500, 100, null, null, null, "GHTK")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => rule.Update(" ", 1, true, null, null, null, null, null, null, "GHTK")).Should().Throw<ArgumentException>();
    }

    private static Shipment NewShipment() => Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "GHTK",
        new ShipmentRecipient("An", "0900000000", "1 Le Loi", null, "Quan 1", "Ho Chi Minh", null),
        new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Shirt", 2, 0.4m) });

    [Fact]
    public void A_carrier_that_skips_steps_still_walks_the_shipment_forward_and_delivery_raises_the_event_once()
    {
        var shipment = NewShipment();
        shipment.ClearDomainEvents();

        shipment.ApplyCarrierStatus(CarrierShipmentStatus.Delivered, "Quan 1", "delivered");

        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.DomainEvents.OfType<ERP.SHP.Domain.Events.DeliveryConfirmedEvent>().Should().ContainSingle();

        shipment.ClearDomainEvents();
        shipment.ApplyCarrierStatus(CarrierShipmentStatus.Delivered, "Quan 1", "delivered again");
        shipment.ApplyCarrierStatus(CarrierShipmentStatus.InTransit, "x", "late message");
        shipment.DomainEvents.Should().BeEmpty("a final state never changes");
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
    }

    [Fact]
    public void A_late_in_transit_message_never_moves_a_shipment_back()
    {
        var shipment = NewShipment();
        shipment.ApplyCarrierStatus(CarrierShipmentStatus.OutForDelivery, "Quan 1", "out");

        shipment.ApplyCarrierStatus(CarrierShipmentStatus.InTransit, "Hub", "late");

        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
        shipment.TrackingEvents.Select(e => e.Status).Should().Contain("InTransit");
    }

    [Fact]
    public void A_failed_delivery_then_a_return_ends_the_shipment_and_a_cancel_after_pickup_is_still_recorded()
    {
        var failed = NewShipment();
        failed.ApplyCarrierStatus(CarrierShipmentStatus.DeliveryFailed, "Quan 1", "nobody home");
        failed.Status.Should().Be(ShipmentStatus.DeliveryFailed);
        failed.ApplyCarrierStatus(CarrierShipmentStatus.Returning, "Hub", "on the way back");
        failed.Status.Should().Be(ShipmentStatus.DeliveryFailed);
        failed.ApplyCarrierStatus(CarrierShipmentStatus.Returned, "Shop", "returned");
        failed.Status.Should().Be(ShipmentStatus.Returned);

        var cancelled = NewShipment();
        cancelled.ApplyCarrierStatus(CarrierShipmentStatus.Cancelled, "", "cancelled by the shop");
        cancelled.Status.Should().Be(ShipmentStatus.Cancelled);
        cancelled.CancelReason.Should().Contain("cancelled by the shop");
    }

    [Fact]
    public void A_booking_keeps_the_carrier_codes_fee_and_reason_and_clears_an_earlier_failure()
    {
        var shipment = NewShipment();
        var carrier = Guid.NewGuid();
        shipment.RecordBookingFailure("GHTK: address not covered");

        shipment.RecordBooking(carrier, "Viettel Post", "VTP123", "ORD-9", 32000m, DateTime.UtcNow.AddDays(2), "cheapest of 2 quotes");

        shipment.CarrierId.Should().Be(carrier);
        shipment.TrackingNumber.Should().Be("VTP123");
        shipment.CarrierFee.Should().Be(32000m);
        shipment.BookingError.Should().BeNull();
        shipment.SelectionNote.Should().Contain("cheapest");
    }
}
