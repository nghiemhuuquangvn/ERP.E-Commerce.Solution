using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Infrastructure.BackgroundJobs;
using ERP.SHP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11: carrier notifications (one verified path), the polling safety net, and cancelling at the carrier.</summary>
public class CarrierTrackingTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly CarrierCredentials Creds = new("GHTK", "P", null, "T", "SECRET", true);

    private readonly Mock<IShippingUnitOfWork> _uow = new();
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<ICarrierRepository> _carriers = new();
    private readonly Mock<ICarrierProviderResolver> _resolver = new();
    private readonly Mock<ICarrierProvider> _provider = new();
    private readonly List<CarrierWebhookLog> _logs = new();
    private readonly Carrier _row = Carrier.Create("GHTK", "GHTK", 0m);

    public CarrierTrackingTests()
    {
        _uow.SetupGet(u => u.Shipments).Returns(_shipments.Object);
        _uow.SetupGet(u => u.Carriers).Returns(_carriers.Object);
        _carriers.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(_row);
        _provider.SetupGet(p => p.Code).Returns("GHTK");
        _provider.SetupGet(p => p.Implemented).Returns(true);
        _provider.Setup(p => p.WebhookReply(It.IsAny<bool>(), It.IsAny<string>())).Returns((bool ok, string m) => new CarrierWebhookReply(ok ? 200 : 400, new { m }));
        _provider.Setup(p => p.PeekTrackingCode(It.IsAny<CarrierWebhookRequest>())).Returns("TRK1");
        _resolver.Setup(r => r.Provider("GHTK")).Returns(_provider.Object);
        _resolver.Setup(r => r.ResolveAsync("GHTK", It.IsAny<CancellationToken>())).ReturnsAsync(new ResolvedCarrier(_provider.Object, Creds));
        _shipments.Setup(s => s.AddWebhookLogAsync(It.IsAny<CarrierWebhookLog>(), It.IsAny<CancellationToken>()))
            .Callback<CarrierWebhookLog, CancellationToken>((l, _) => _logs.Add(l)).Returns(Task.CompletedTask);
    }

    private CarrierTrackingService Tracking() => new(_uow.Object, _resolver.Object, NullLogger<CarrierTrackingService>.Instance);
    private CarrierWebhookService Webhooks() => new(_uow.Object, _resolver.Object, Tracking(), NullLogger<CarrierWebhookService>.Instance);

    private Shipment Booked(decimal cod = 300_000m, string tracking = "TRK1")
    {
        var s = Shipment.Create(Guid.NewGuid(), _row.Id, "GHTK", new ShipmentRecipient("An", "0911", "2 Le Loi", null, "Quan 3", "HCM", null),
            new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Shirt", 1, 0.5m) });
        s.SetCommercials(cod, cod);
        s.AssignTrackingNumber(tracking);
        s.ClearDomainEvents();
        _shipments.Setup(r => r.FindTenantByTrackingNumberAsync(tracking, It.IsAny<CancellationToken>())).ReturnsAsync(Tenant);
        _shipments.Setup(r => r.GetTrackedByTrackingNumberAsync(tracking, It.IsAny<CancellationToken>())).ReturnsAsync(s);
        return s;
    }

    private void Verified(CarrierShipmentStatus status, string carrierStatus = "5", decimal? cod = 300_000m, string tracking = "TRK1") =>
        _provider.Setup(p => p.VerifyWebhook(Creds, It.IsAny<CarrierWebhookRequest>()))
            .Returns(new CarrierWebhookEvent(true, null, tracking, carrierStatus, status, new DateTime(2026, 10, 29, 3, 0, 0, DateTimeKind.Utc), "ok", cod));

    private static CarrierWebhookRequest Req() => new("POST", new Dictionary<string, string>(), "{}");

    // ── webhook ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_verified_delivery_notification_delivers_the_shipment_logs_it_and_raises_delivery_confirmed()
    {
        var shipment = Booked();
        Verified(CarrierShipmentStatus.Delivered);

        var result = await Webhooks().ProcessAsync("ghtk", Req());

        result.Outcome.Should().Be(CarrierWebhookOutcome.Applied);
        result.Reply.StatusCode.Should().Be(200);
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.DomainEvents.OfType<ERP.SHP.Domain.Events.DeliveryConfirmedEvent>().Should().ContainSingle();
        shipment.ReviewNote.Should().BeNull();
        _logs.Should().ContainSingle().Which.Should().Match<CarrierWebhookLog>(l => l.TrackingCode == "TRK1" && l.CarrierStatus == "5" && l.TenantId == Tenant);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_same_notification_again_changes_nothing()
    {
        var shipment = Booked();
        Verified(CarrierShipmentStatus.Delivered);
        _shipments.Setup(s => s.WebhookSeenAsync("GHTK", "TRK1", "5", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Webhooks().ProcessAsync("GHTK", Req());

        result.Outcome.Should().Be(CarrierWebhookOutcome.Duplicate);
        result.Reply.StatusCode.Should().Be(200, "the carrier must stop retrying");
        shipment.Status.Should().Be(ShipmentStatus.Pending);
        _logs.Should().BeEmpty();
    }

    [Fact]
    public async Task A_notification_that_fails_verification_or_names_another_parcel_is_refused_and_applies_nothing()
    {
        var shipment = Booked();
        _provider.Setup(p => p.VerifyWebhook(Creds, It.IsAny<CarrierWebhookRequest>()))
            .Returns(new CarrierWebhookEvent(false, "bad secret", null, "", CarrierShipmentStatus.Accepted, DateTime.UtcNow, null, null));
        (await Webhooks().ProcessAsync("GHTK", Req())).Outcome.Should().Be(CarrierWebhookOutcome.Rejected);

        Verified(CarrierShipmentStatus.Delivered, tracking: "OTHER");   // verified, but not the parcel that was peeked
        (await Webhooks().ProcessAsync("GHTK", Req())).Reply.StatusCode.Should().Be(400);

        shipment.Status.Should().Be(ShipmentStatus.Pending);
        _logs.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_carriers_unknown_parcels_and_shops_that_cannot_verify_are_answered_not_applied()
    {
        (await Webhooks().ProcessAsync("fedex", Req())).Outcome.Should().Be(CarrierWebhookOutcome.UnknownCarrier);

        _shipments.Setup(r => r.FindTenantByTrackingNumberAsync("TRK1", It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        (await Webhooks().ProcessAsync("GHTK", Req())).Outcome.Should().Be(CarrierWebhookOutcome.UnknownShipment);

        _shipments.Setup(r => r.FindTenantByTrackingNumberAsync("TRK1", It.IsAny<CancellationToken>())).ReturnsAsync(Tenant);
        _resolver.Setup(r => r.ResolveAsync("GHTK", It.IsAny<CancellationToken>())).ThrowsAsync(new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Carrier", "GHTK is not enabled"));
        (await Webhooks().ProcessAsync("GHTK", Req())).Outcome.Should().Be(CarrierWebhookOutcome.NotVerifiable);
    }

    [Fact]
    public async Task Cash_collected_that_differs_from_the_order_is_flagged_for_a_person()
    {
        var shipment = Booked(cod: 300_000m);
        Verified(CarrierShipmentStatus.Delivered, cod: 250_000m);

        await Webhooks().ProcessAsync("GHTK", Req());

        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.ReviewNote.Should().Contain("250").And.Contain("300");
    }

    [Fact]
    public async Task A_cancelled_status_cancels_the_shipment_and_a_failed_delivery_keeps_it_open()
    {
        var cancelled = Booked();
        Verified(CarrierShipmentStatus.Cancelled, "-1");
        await Webhooks().ProcessAsync("GHTK", Req());
        cancelled.Status.Should().Be(ShipmentStatus.Cancelled);

        var failed = Booked(tracking: "TRK2");
        _provider.Setup(p => p.PeekTrackingCode(It.IsAny<CarrierWebhookRequest>())).Returns("TRK2");
        Verified(CarrierShipmentStatus.DeliveryFailed, "9", tracking: "TRK2");
        await Webhooks().ProcessAsync("GHTK", Req());
        failed.Status.Should().Be(ShipmentStatus.DeliveryFailed);
    }

    // ── cancel at the carrier ─────────────────────────────────────────────

    [Fact]
    public async Task A_parcel_not_yet_collected_is_cancelled_at_the_carrier_and_here()
    {
        var shipment = Booked();
        _provider.Setup(p => p.CancelAsync(Creds, "TRK1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CarrierCancelResult(true, null));

        var (done, problem) = await Tracking().CancelAtCarrierAsync(shipment, "customer cancelled");

        (done, problem).Should().Be((true, null));
        shipment.Status.Should().Be(ShipmentStatus.Cancelled);
        _provider.Verify(p => p.CancelAsync(Creds, "TRK1", "customer cancelled", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_the_carrier_refuses_to_cancel_the_shipment_stays_and_a_person_is_told()
    {
        var shipment = Booked();
        _provider.Setup(p => p.CancelAsync(Creds, "TRK1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CarrierCancelResult(false, "already picked up"));

        var (done, problem) = await Tracking().CancelAtCarrierAsync(shipment, "x");

        done.Should().BeFalse();
        problem.Should().Contain("already picked up");
        shipment.Status.Should().Be(ShipmentStatus.Pending);
        shipment.ReviewNote.Should().Contain("already picked up");
    }

    [Fact]
    public async Task A_parcel_the_carrier_already_has_is_not_cancelled_and_one_never_booked_is_just_cancelled_here()
    {
        var picked = Booked();
        picked.MarkAsPickedUp();
        (await Tracking().CancelAtCarrierAsync(picked, "x")).Cancelled.Should().BeFalse();
        picked.Status.Should().Be(ShipmentStatus.PickedUp);
        _provider.Verify(p => p.CancelAsync(It.IsAny<CarrierCredentials>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var unbooked = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "x", new ShipmentRecipient("A", null, "s", null, "d", "p", null),
            new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "i", 1, 1m) });
        (await Tracking().CancelAtCarrierAsync(unbooked, "x")).Cancelled.Should().BeTrue();
        unbooked.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public async Task A_cancelled_order_cancels_waiting_parcels_flags_parcels_on_the_road_and_repeats_harmlessly()
    {
        var waiting = Booked(tracking: "W1");
        var onTheRoad = Booked(tracking: "R1");
        onTheRoad.MarkAsPickedUp();
        var orderId = Guid.NewGuid();
        _shipments.Setup(s => s.GetTrackedByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { waiting, onTheRoad });
        _provider.Setup(p => p.CancelAsync(Creds, "W1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CarrierCancelResult(true, null));
        var handler = new CancelOrderShipmentsCommandHandler(_uow.Object, Tracking(), NullLogger<CancelOrderShipmentsCommandHandler>.Instance);

        (await handler.Handle(new CancelOrderShipmentsCommand { OrderId = orderId }, default)).Should().Be(1);
        (await handler.Handle(new CancelOrderShipmentsCommand { OrderId = orderId }, default)).Should().Be(0);

        waiting.Status.Should().Be(ShipmentStatus.Cancelled);
        onTheRoad.Status.Should().Be(ShipmentStatus.PickedUp);
        onTheRoad.ReviewNote.Should().Contain("have it returned");
        _provider.Verify(p => p.CancelAsync(Creds, "W1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── polling safety net ────────────────────────────────────────────────

    [Fact]
    public async Task The_sync_job_asks_the_carrier_about_a_parcel_nobody_reported_and_applies_the_answer()
    {
        var shipment = Booked();
        _shipments.Setup(s => s.GetTenantsWithOpenShipmentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Guid> { Tenant });
        _shipments.Setup(s => s.GetStaleInCarrierHandsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { shipment });
        _provider.Setup(p => p.GetStatusAsync(Creds, "TRK1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CarrierStatusResult(true, "5", CarrierShipmentStatus.Delivered, "delivered", null, null));

        var services = new ServiceCollection();
        services.AddSingleton(_uow.Object);
        services.AddSingleton(_resolver.Object);
        services.AddSingleton(Tracking());
        var job = new CarrierTrackingSyncJob(services.BuildServiceProvider(), NullLogger<CarrierTrackingSyncJob>.Instance);

        var asked = await job.RunOnceAsync(TimeSpan.FromHours(6), default);

        asked.Should().Be(1);
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.LastCarrierSyncAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_carrier_that_cannot_be_polled_is_not_asked_again_every_pass()
    {
        var shipment = Booked();
        _shipments.Setup(s => s.GetTenantsWithOpenShipmentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Guid> { Tenant });
        _shipments.Setup(s => s.GetStaleInCarrierHandsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { shipment });
        _provider.Setup(p => p.GetStatusAsync(Creds, "TRK1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null, "no polling"));
        var services = new ServiceCollection();
        services.AddSingleton(_uow.Object);
        services.AddSingleton(_resolver.Object);
        services.AddSingleton(Tracking());

        await new CarrierTrackingSyncJob(services.BuildServiceProvider(), NullLogger<CarrierTrackingSyncJob>.Instance).RunOnceAsync(TimeSpan.FromHours(6), default);

        shipment.Status.Should().Be(ShipmentStatus.Pending);
        shipment.LastCarrierSyncAt.Should().NotBeNull("it is marked so the next pass skips it");
    }
}
