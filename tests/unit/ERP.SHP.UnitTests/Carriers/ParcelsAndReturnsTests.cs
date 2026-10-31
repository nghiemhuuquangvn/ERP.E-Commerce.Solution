using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Application.Commands;
using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Queries;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11d: an order in several parcels, and the way back of a returned order through the carrier.</summary>
public class ParcelsAndReturnsTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Shirt = Guid.NewGuid();
    private static readonly Guid Hat = Guid.NewGuid();

    private readonly Mock<IShippingUnitOfWork> _uow = new();
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<IReturnShipmentRepository> _returns = new();
    private readonly Mock<ICarrierRepository> _carriers = new();
    private readonly Mock<IShippingConfigRepository> _config = new();
    private readonly Mock<ICarrierProviderResolver> _resolver = new();
    private readonly FakeCarrier _carrier = new();
    private readonly List<Shipment> _added = new();
    private readonly List<ReturnShipment> _addedReturns = new();

    private sealed class FakeCarrier : ICarrierProvider
    {
        public string Code => CarrierCodes.Ghtk;
        public bool Implemented => true;
        public List<CarrierBookRequest> Booked { get; } = new();
        public List<string> Cancelled { get; } = new();
        public string? RefuseBookingNumber { get; set; }     // refuse the Nth booking (1-based)
        public bool RefuseCancel { get; set; }

        public Task<CarrierQuote> QuoteAsync(CarrierCredentials c, CarrierQuoteRequest r, CancellationToken ct = default) =>
            Task.FromResult(new CarrierQuote(Code, true, 20_000m, null, null, null));

        public Task<CarrierBooking> BookAsync(CarrierCredentials c, CarrierBookRequest r, CancellationToken ct = default)
        {
            Booked.Add(r);
            if (RefuseBookingNumber == Booked.Count.ToString()) return Task.FromResult(new CarrierBooking(false, null, null, null, null, "address not served"));
            return Task.FromResult(new CarrierBooking(true, $"TRK-{Booked.Count}", $"ORD-{Booked.Count}", 20_000m, null, null));
        }

        public Task<CarrierCancelResult> CancelAsync(CarrierCredentials c, string t, string reason, CancellationToken ct = default)
        {
            if (RefuseCancel) return Task.FromResult(new CarrierCancelResult(false, "already picked up"));
            Cancelled.Add(t);
            return Task.FromResult(new CarrierCancelResult(true, null));
        }

        public Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials c, string t, CancellationToken ct = default) => throw new NotSupportedException();
        public string? PeekTrackingCode(CarrierWebhookRequest r) => null;
        public CarrierWebhookEvent VerifyWebhook(CarrierCredentials c, CarrierWebhookRequest r) => throw new NotSupportedException();
        public CarrierWebhookReply WebhookReply(bool accepted, string message) => new(200, null);
    }

    public ParcelsAndReturnsTests()
    {
        _uow.SetupGet(u => u.Shipments).Returns(_shipments.Object);
        _uow.SetupGet(u => u.ReturnShipments).Returns(_returns.Object);
        _uow.SetupGet(u => u.Carriers).Returns(_carriers.Object);
        _uow.SetupGet(u => u.Config).Returns(_config.Object);
        var carrierRow = Carrier.Create("GHTK", CarrierCodes.Ghtk, 0m);
        _carriers.Setup(c => c.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(carrierRow);
        _carriers.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(carrierRow);
        _config.Setup(c => c.GetActiveRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CarrierSelectionRule>());
        var settings = TenantShippingSettings.CreateDefault(Tenant);
        settings.UpdateSender("Shop", "0900000001", "1 Nguyen Hue", "Ben Nghe", "Quan 1", "Ho Chi Minh", 500);
        _config.Setup(c => c.GetTenantSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _resolver.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<ResolvedCarrier> { new(_carrier, new CarrierCredentials(CarrierCodes.Ghtk, "x", null, "s", null, true)) });
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ResolvedCarrier(_carrier, new CarrierCredentials(CarrierCodes.Ghtk, "x", null, "s", null, true)));
        _shipments.Setup(s => s.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>())).Callback<Shipment, CancellationToken>((s, _) => _added.Add(s)).Returns(Task.CompletedTask);
        _returns.Setup(r => r.AddAsync(It.IsAny<ReturnShipment>(), It.IsAny<CancellationToken>())).Callback<ReturnShipment, CancellationToken>((r, _) => _addedReturns.Add(r)).Returns(Task.CompletedTask);
        _returns.Setup(r => r.GetTrackedByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _addedReturns.ToList());
    }

    private CarrierBookingService Booking() => new(_uow.Object, _resolver.Object, NullLogger<CarrierBookingService>.Instance);
    private CarrierTrackingService Tracking() => new(_uow.Object, _resolver.Object, NullLogger<CarrierTrackingService>.Instance);

    /// <summary>A booked parcel of 3 shirts + 1 hat worth 400,000 with 430,000 cash on delivery (30,000 delivery fee).</summary>
    private static Shipment Parcel(Guid? orderId = null)
    {
        var s = Shipment.Create(orderId ?? Guid.NewGuid(), Guid.NewGuid(), "GHTK",
            new ShipmentRecipient("An", "0911111111", "2 Le Loi", "Phuong 1", "Quan 3", "Ho Chi Minh", null),
            new List<(Guid, string, int, decimal)> { (Shirt, "Shirt", 3, 0.3m), (Hat, "Hat", 1, 0.2m) });
        s.SetCommercials(430_000m, 400_000m);
        s.RecordBooking(Guid.NewGuid(), "GHTK", "OLD-TRACK", "OLD-ORD", 20_000m, null, "test");
        return s;
    }

    private static ParcelSplitPart Part(decimal? cod, params (Guid, int)[] lines) =>
        new(lines.Select(l => new ParcelSplitLine(l.Item1, l.Item2)).ToList(), cod);

    // ---- the plan ----

    [Fact]
    public void A_split_shares_cash_and_value_by_units_and_the_last_parcel_takes_the_remainder()
    {
        var plan = ParcelSplitPlanner.Plan(Parcel(), new[] { Part(null, (Shirt, 2)), Part(null, (Shirt, 1), (Hat, 1)) });

        plan.Should().HaveCount(2);
        plan[0].CodAmount.Should().Be(Math.Round(430_000m * 2 / 4, 0));
        plan.Sum(p => p.CodAmount).Should().Be(430_000m);
        plan.Sum(p => p.DeclaredValue).Should().Be(400_000m);
        plan[0].Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public void A_split_with_cash_typed_per_parcel_must_add_up_to_the_parcels_cash()
    {
        var ok = ParcelSplitPlanner.Plan(Parcel(), new[] { Part(330_000m, (Shirt, 3)), Part(100_000m, (Hat, 1)) });
        ok.Select(p => p.CodAmount).Should().Equal(330_000m, 100_000m);

        var bad = () => ParcelSplitPlanner.Plan(Parcel(), new[] { Part(330_000m, (Shirt, 3)), Part(90_000m, (Hat, 1)) });
        bad.Should().Throw<BusinessRuleException>().WithMessage("*must equal*");
        var half = () => ParcelSplitPlanner.Plan(Parcel(), new[] { Part(330_000m, (Shirt, 3)), Part(null, (Hat, 1)) });
        half.Should().Throw<BusinessRuleException>().WithMessage("*every parcel or of none*");
    }

    [Theory]
    [InlineData(2, 0, 1)]   // an item lost
    [InlineData(3, 1, 1)]   // an item invented
    public void Every_item_must_go_to_exactly_one_parcel(int shirtsInFirst, int shirtsInSecond, int hats)
    {
        var parts = new[] { Part(null, (Shirt, shirtsInFirst)), Part(null, (Shirt, shirtsInSecond), (Hat, hats)) };
        var act = () => ParcelSplitPlanner.Plan(Parcel(), parts);
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void A_split_needs_two_parcels_and_no_empty_one()
    {
        var one = () => ParcelSplitPlanner.Plan(Parcel(), new[] { Part(null, (Shirt, 3), (Hat, 1)) });
        one.Should().Throw<BusinessRuleException>().WithMessage("*at least two*");
        var empty = () => ParcelSplitPlanner.Plan(Parcel(), new[] { Part(null, (Shirt, 3), (Hat, 1)), Part(null, (Shirt, 0)) });
        empty.Should().Throw<BusinessRuleException>().WithMessage("*at least one item*");
    }

    // ---- the parcel itself ----

    [Fact]
    public void A_parcel_of_a_split_order_has_its_own_reference_and_tells_the_parcel_count_when_delivered()
    {
        var s = Parcel();
        s.BookingReference.Should().Be($"SO-{s.OrderId:N}");
        s.SetParcel(2, 3);
        s.BookingReference.Should().Be($"SO-{s.OrderId:N}-P2");

        s.ApplyCarrierStatus(CarrierShipmentStatus.Delivered, "x", "delivered");
        var delivered = s.DomainEvents.OfType<DeliveryConfirmedEvent>().Single();
        (delivered.ParcelNo, delivered.ParcelCount, delivered.CodAmount).Should().Be((2, 3, 430_000m));
    }

    [Fact]
    public async Task A_delivered_parcel_tells_how_many_other_parcels_of_the_order_are_still_on_the_way()
    {
        var orderId = Guid.NewGuid();
        var first = Parcel(orderId); first.SetParcel(1, 2);
        var second = Parcel(orderId); second.SetParcel(2, 2);
        _shipments.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { first, second });

        await Tracking().ApplyAsync(first, CarrierShipmentStatus.Delivered, "5", null, null);
        first.DomainEvents.OfType<DeliveryConfirmedEvent>().Single().OtherParcelsPending.Should().Be(1);

        await Tracking().ApplyAsync(second, CarrierShipmentStatus.Delivered, "5", null, null);
        second.DomainEvents.OfType<DeliveryConfirmedEvent>().Single().OtherParcelsPending.Should().Be(0);   // the last one
    }

    [Fact]
    public async Task The_goods_of_a_return_are_home_only_when_no_other_reverse_parcel_is_pending()
    {
        var orderId = Guid.NewGuid();
        var a = ReturnShipment.Create(Guid.NewGuid(), orderId, "x");
        var b = ReturnShipment.Create(Guid.NewGuid(), orderId, "x");
        _addedReturns.AddRange(new[] { a, b });

        await Tracking().ApplyReturnAsync(a, CarrierShipmentStatus.Delivered);
        a.DomainEvents.OfType<ReturnReceivedEvent>().Single().OtherReturnsPending.Should().Be(1);
        await Tracking().ApplyReturnAsync(b, CarrierShipmentStatus.Delivered);
        b.DomainEvents.OfType<ReturnReceivedEvent>().Single().OtherReturnsPending.Should().Be(0);
    }

    // ---- the split command ----

    private SplitShipmentCommandHandler Splitter() => new(_uow.Object, Booking(), Tracking(), NullLogger<SplitShipmentCommandHandler>.Instance);

    private Shipment StageParcel()
    {
        var s = Parcel();
        _shipments.Setup(r => r.GetByIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);
        _shipments.Setup(r => r.GetTrackedByOrderIdAsync(s.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { s });
        return s;
    }

    [Fact]
    public async Task Splitting_books_every_new_parcel_cancels_the_old_one_and_saves()
    {
        var old = StageParcel();
        var result = await Splitter().Handle(new SplitShipmentCommand
        {
            ShipmentId = old.Id,
            Parts = { Part(null, (Shirt, 2)), Part(null, (Shirt, 1), (Hat, 1)) }
        }, default);

        result.Should().HaveCount(2);
        _carrier.Booked.Select(b => b.OrderReference).Should().Equal($"SO-{old.OrderId:N}-P1", $"SO-{old.OrderId:N}-P2");
        _carrier.Booked.Sum(b => b.CodAmount).Should().Be(430_000m);
        _carrier.Cancelled.Should().Equal("OLD-TRACK");
        old.Status.Should().Be(ShipmentStatus.Cancelled);
        _added.Should().HaveCount(2).And.OnlyContain(s => s.ParcelCount == 2 && s.TrackingNumber != null);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_a_new_parcel_is_refused_the_earlier_bookings_are_undone_and_the_old_parcel_stays()
    {
        var old = StageParcel();
        _carrier.RefuseBookingNumber = "2";

        var act = () => Splitter().Handle(new SplitShipmentCommand { ShipmentId = old.Id, Parts = { Part(null, (Shirt, 2)), Part(null, (Shirt, 1), (Hat, 1)) } }, default);

        await act.Should().ThrowAsync<CarrierBookingException>();
        _carrier.Cancelled.Should().Equal("TRK-1");          // the first new booking was undone
        old.Status.Should().Be(ShipmentStatus.ReadyForPickup.Equals(old.Status) ? ShipmentStatus.ReadyForPickup : ShipmentStatus.Pending);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_the_carrier_will_not_cancel_the_old_parcel_nothing_is_split()
    {
        var old = StageParcel();
        _carrier.RefuseCancel = true;

        var act = () => Splitter().Handle(new SplitShipmentCommand { ShipmentId = old.Id, Parts = { Part(null, (Shirt, 2)), Part(null, (Shirt, 1), (Hat, 1)) } }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*did not cancel*");
        _added.Should().BeEmpty();
        old.Status.Should().NotBe(ShipmentStatus.Cancelled);
    }

    [Fact]
    public async Task A_parcel_the_carrier_has_collected_cannot_be_split()
    {
        var old = StageParcel();
        old.MarkAsPickedUp();
        var act = () => Splitter().Handle(new SplitShipmentCommand { ShipmentId = old.Id, Parts = { Part(null, (Shirt, 2)), Part(null, (Shirt, 1), (Hat, 1)) } }, default);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*cannot be split*");
    }

    // ---- the way back ----

    private ReturnBookingService Returner() => new(_uow.Object, Booking(), NullLogger<ReturnBookingService>.Instance);

    private Shipment Delivered()
    {
        var s = Parcel();
        s.ApplyCarrierStatus(CarrierShipmentStatus.Delivered, "x", "delivered");
        _shipments.Setup(r => r.GetTrackedByOrderIdAsync(s.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { s });
        return s;
    }

    [Fact]
    public async Task A_return_is_booked_as_the_forward_parcel_turned_round_with_no_cash()
    {
        var parcel = Delivered();
        var booked = await Returner().BookReturnsAsync(parcel.OrderId, "wrong size");

        var r = booked.Single();
        r.TrackingNumber.Should().Be("TRK-1");
        r.BookingError.Should().BeNull();
        var request = _carrier.Booked.Single();
        request.OrderReference.Should().Be($"RT-{parcel.OrderId:N}-P1");
        request.CodAmount.Should().Be(0m);
        request.From.Street.Should().Be("2 Le Loi");        // the customer is the sender
        request.To.Street.Should().Be("1 Nguyen Hue");      // the shop's pickup address receives
        request.Items.Sum(i => i.Quantity).Should().Be(4);
    }

    [Fact]
    public async Task A_refused_return_booking_is_kept_on_the_return_and_a_second_approve_books_only_what_is_missing()
    {
        var parcel = Delivered();
        _carrier.RefuseBookingNumber = "1";
        var first = (await Returner().BookReturnsAsync(parcel.OrderId, "wrong size")).Single();
        first.TrackingNumber.Should().BeNull();
        first.BookingError.Should().Contain("address not served");

        var second = (await Returner().BookReturnsAsync(parcel.OrderId, "wrong size")).Single();
        second.Should().BeSameAs(first);                    // the same return, not a second one
        second.TrackingNumber.Should().NotBeNull();
        second.BookingError.Should().BeNull();

        var third = await Returner().BookReturnsAsync(parcel.OrderId, "wrong size");
        _carrier.Booked.Should().HaveCount(2);              // nothing booked again once it has a tracking code
        third.Single().TrackingNumber.Should().Be(second.TrackingNumber);
    }

    [Fact]
    public async Task An_order_with_nothing_delivered_has_nothing_to_fetch_back()
    {
        var s = Parcel();
        _shipments.Setup(r => r.GetTrackedByOrderIdAsync(s.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { s });
        var act = () => Returner().BookReturnsAsync(s.OrderId, "x");
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no delivered parcel*");
    }

    [Fact]
    public void The_carrier_delivering_the_reverse_parcel_means_the_goods_are_received()
    {
        var r = ReturnShipment.Create(Guid.NewGuid(), Guid.NewGuid(), "x");
        r.ApplyCarrierStatus(CarrierShipmentStatus.PickedUp);
        r.Status.Should().Be(ReturnShipmentStatus.PickedUp);

        r.ApplyCarrierStatus(CarrierShipmentStatus.Delivered);
        r.Status.Should().Be(ReturnShipmentStatus.Received);
        r.DomainEvents.OfType<ReturnReceivedEvent>().Should().ContainSingle();

        r.ApplyCarrierStatus(CarrierShipmentStatus.Cancelled);   // final states never change
        r.Status.Should().Be(ReturnShipmentStatus.Received);
    }
    // ---- the admin Returns screen (ROADMAP P2-1b) ----

    private async Task<ReturnShipment> BookedReturn()
    {
        var parcel = Delivered();
        var r = (await Returner().BookReturnsAsync(parcel.OrderId, "wrong size")).Single();
        _returns.Setup(x => x.GetByIdAsync(r.Id, It.IsAny<CancellationToken>())).ReturnsAsync(r);
        return r;
    }

    [Fact]
    public async Task Book_again_books_a_refused_return_and_leaves_a_booked_one_alone()
    {
        var parcel = Delivered();
        _carrier.RefuseBookingNumber = "1";
        var r = (await Returner().BookReturnsAsync(parcel.OrderId, "wrong size")).Single();
        _returns.Setup(x => x.GetByIdAsync(r.Id, It.IsAny<CancellationToken>())).ReturnsAsync(r);
        var handler = new RebookReturnCommandHandler(_uow.Object, Returner());

        var again = await handler.Handle(new RebookReturnCommand { ReturnShipmentId = r.Id }, default);
        again.TrackingNumber.Should().Be("TRK-2");
        again.BookingError.Should().BeNull();

        await handler.Handle(new RebookReturnCommand { ReturnShipmentId = r.Id }, default);
        _carrier.Booked.Should().HaveCount(2);            // the second click booked nothing
    }

    [Fact]
    public async Task A_received_or_cancelled_return_cannot_be_booked_again()
    {
        var r = await BookedReturn();
        r.MarkReceived();
        var act = () => new RebookReturnCommandHandler(_uow.Object, Returner()).Handle(new RebookReturnCommand { ReturnShipmentId = r.Id }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Cancelling_a_booked_return_cancels_the_pickup_at_the_carrier_first()
    {
        var r = await BookedReturn();

        var dto = await new CancelReturnAtCarrierCommandHandler(_uow.Object, Tracking()).Handle(new CancelReturnAtCarrierCommand { ReturnShipmentId = r.Id }, default);

        dto.Status.Should().Be("Cancelled");
        _carrier.Cancelled.Should().ContainSingle().Which.Should().Be("TRK-1");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task When_the_carrier_will_not_cancel_the_pickup_the_return_stays_open_with_the_reason()
    {
        var r = await BookedReturn();
        _carrier.RefuseCancel = true;

        var act = () => new CancelReturnAtCarrierCommandHandler(_uow.Object, Tracking()).Handle(new CancelReturnAtCarrierCommand { ReturnShipmentId = r.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already picked up*");
        r.Status.Should().Be(ReturnShipmentStatus.Requested);
    }

    [Fact]
    public async Task A_return_whose_goods_the_carrier_already_collected_cannot_be_cancelled()
    {
        var r = await BookedReturn();
        r.MarkPickedUp();

        var act = () => new CancelReturnAtCarrierCommandHandler(_uow.Object, Tracking()).Handle(new CancelReturnAtCarrierCommand { ReturnShipmentId = r.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already PickedUp*");
        _carrier.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task A_return_without_a_carrier_booking_is_cancelled_here_only()
    {
        var parcel = Delivered();
        _carrier.RefuseBookingNumber = "1";
        var r = (await Returner().BookReturnsAsync(parcel.OrderId, "x")).Single();
        _returns.Setup(x => x.GetByIdAsync(r.Id, It.IsAny<CancellationToken>())).ReturnsAsync(r);

        var dto = await new CancelReturnAtCarrierCommandHandler(_uow.Object, Tracking()).Handle(new CancelReturnAtCarrierCommand { ReturnShipmentId = r.Id }, default);

        dto.Status.Should().Be("Cancelled");
        _carrier.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task The_returns_list_pages_filters_and_refuses_an_unknown_status()
    {
        var r = ReturnShipment.Create(Guid.NewGuid(), Guid.NewGuid(), "x");
        _returns.Setup(x => x.ListAsync(100, 100, ReturnShipmentStatus.Requested, "TRK", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ReturnShipment> { r }, 41));
        var handler = new ListReturnsQueryHandler(_returns.Object);

        var page = await handler.Handle(new ListReturnsQuery { Page = 2, PageSize = 5000, Status = "requested", Search = "TRK", NeedsAttention = true }, default);

        page.TotalCount.Should().Be(41);
        page.PageNumber.Should().Be(2);
        page.PageSize.Should().Be(100);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(r.Id);

        var act = () => handler.Handle(new ListReturnsQuery { Status = "Floating" }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
