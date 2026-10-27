using ERP.Shared.Core.Domain;
using ERP.SHP.Application.Commands;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Events;
using ERP.SHP.Infrastructure.EventBus;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.EventBus;

/// <summary>ROADMAP P1-5: the saga's shipment command is answered, never dropped, and never creates a second shipment.</summary>
public class SagaShipmentRequestProcessorTests
{
    private readonly Mock<IShippingUnitOfWork> _uow = new();
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<ICarrierRepository> _carriers = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<IDomainEvent> _staged = new();

    public SagaShipmentRequestProcessorTests()
    {
        _weights.Setup(w => w.GetWeightsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<Guid, int>());
        _uow.SetupGet(u => u.Shipments).Returns(_shipments.Object);
        _uow.SetupGet(u => u.Carriers).Returns(_carriers.Object);
        _uow.Setup(u => u.StageEventAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => _staged.Add(e)).Returns(Task.CompletedTask);
        _shipments.Setup(s => s.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment>());
        _carriers.Setup(c => c.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Carrier> { Carrier.Create("GHN", "GHN", 10m) });
    }

    private readonly Mock<IProductWeightClient> _weights = new();

    private SagaShipmentRequestProcessor Processor() => new(_uow.Object, _mediator.Object, NullLogger<SagaShipmentRequestProcessor>.Instance, _weights.Object);

    [Fact]
    public async Task The_products_own_weight_goes_on_the_line_and_a_product_without_one_keeps_zero_for_the_shop_default()
    {
        var command = Command();
        var heavy = command.Items[0].ProductId;
        var unknown = Guid.NewGuid();
        command.Items.Add(new SagaShipmentLine { ProductId = unknown, ProductName = "Khong ro", Quantity = 1 });
        _weights.Setup(w => w.GetWeightsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [heavy] = 1250 });
        CreateShipmentCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateShipmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ShipmentDto>, CancellationToken>((c, _) => sent = (CreateShipmentCommand)c);

        await Processor().ProcessAsync(command);

        sent!.Items.Single(i => i.ProductId == heavy).Weight.Should().Be(1.25m);
        sent.Items.Single(i => i.ProductId == unknown).Weight.Should().Be(0m);
    }

    [Fact]
    public async Task The_ward_of_the_order_address_reaches_the_carrier_request()
    {
        var command = Command();
        command.ShippingAddress!.Ward = "Phuong Ben Nghe";

        command.ToCreateShipmentCommand().Recipient.AddressWard.Should().Be("Phuong Ben Nghe");
    }

    private static SagaCreateShipmentCommand Command(bool withAddress = true) => new()
    {
        OrderId = Guid.NewGuid(),
        RecipientName = "Nguyen Van A",
        ShippingAddress = withAddress ? new SagaShippingAddress { Street = "12 Le Loi", City = "Quan 1", State = "Ho Chi Minh", Country = "Vietnam" } : null,
        Items = { new SagaShipmentLine { ProductId = Guid.NewGuid(), ProductName = "Ao thun", Quantity = 2 } },
    };

    [Fact]
    public async Task A_deliverable_command_asks_for_a_shipment_without_naming_a_carrier()
    {
        var command = Command();

        var outcome = await Processor().ProcessAsync(command);

        outcome.Should().Be(SagaShipmentOutcome.Created);
        _mediator.Verify(m => m.Send(It.Is<CreateShipmentCommand>(c => c.OrderId == command.OrderId && c.CarrierId == null && c.Recipient.AddressStreet == "12 Le Loi"), It.IsAny<CancellationToken>()), Times.Once);
        _staged.Should().BeEmpty();
    }

    [Fact]
    public async Task A_command_without_an_address_is_answered_with_a_creation_failure()
    {
        var command = Command(withAddress: false);

        var outcome = await Processor().ProcessAsync(command);

        outcome.Should().Be(SagaShipmentOutcome.Failed);
        var failed = _staged.OfType<ShipmentCreationFailedEvent>().Single();
        (failed.OrderId, failed.Reason).Should().Be((command.OrderId, "the order has no delivery address"));
        _mediator.Verify(m => m.Send(It.IsAny<IRequest<ShipmentDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_no_carrier_accepts_the_parcel_the_failure_carries_every_reason()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateShipmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CarrierBookingException("GHTK: Phone invalid; VTP: Viettel Post does not know the district 'X'"));

        var outcome = await Processor().ProcessAsync(Command());

        outcome.Should().Be(SagaShipmentOutcome.Failed);
        _staged.OfType<ShipmentCreationFailedEvent>().Single().Reason.Should().Contain("GHTK: Phone invalid").And.Contain("VTP:");
    }

    [Fact]
    public async Task A_carrier_that_could_not_be_reached_is_not_a_verdict_the_consumer_retries_it()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateShipmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CarrierBookingException("GHTK: the carrier is not reachable: down", isTransient: true));

        var act = () => Processor().ProcessAsync(Command());

        await act.Should().ThrowAsync<CarrierBookingException>();
        _staged.Should().BeEmpty("no creation failure is announced yet");
    }

    [Fact]
    public async Task An_order_that_already_has_a_shipment_gets_no_second_one_but_the_announcement_again()
    {
        var command = Command();
        var existing = Shipment.Create(command.OrderId, Guid.NewGuid(), "GHN",
            new ShipmentRecipient("A", null, "12 Le Loi", null, "Quan 1", "Ho Chi Minh", null),
            new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Ao", 1, 1m) });
        _shipments.Setup(s => s.GetByOrderIdAsync(command.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { existing });

        var outcome = await Processor().ProcessAsync(command);

        outcome.Should().Be(SagaShipmentOutcome.AlreadyExists);
        _mediator.Verify(m => m.Send(It.IsAny<IRequest<ShipmentDto>>(), It.IsAny<CancellationToken>()), Times.Never);
        var again = _staged.OfType<ShipmentCreatedEvent>().Single();
        (again.ShipmentId, again.OrderId).Should().Be((existing.Id, command.OrderId));
    }

    [Fact]
    public async Task A_cancelled_shipment_does_not_count_so_the_order_can_get_a_new_one()
    {
        var command = Command();
        var cancelled = Shipment.Create(command.OrderId, Guid.NewGuid(), "GHN",
            new ShipmentRecipient("A", null, "12 Le Loi", null, "Quan 1", "Ho Chi Minh", null),
            new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Ao", 1, 1m) });
        cancelled.Cancel("test");
        _shipments.Setup(s => s.GetByOrderIdAsync(command.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Shipment> { cancelled });

        (await Processor().ProcessAsync(command)).Should().Be(SagaShipmentOutcome.Created);
    }

    [Fact]
    public async Task A_technical_failure_that_gave_up_is_reported_with_its_reason()
    {
        var orderId = Guid.NewGuid();

        await Processor().ReportFailureAsync(orderId, "technical failure after 5 attempts: boom");

        var failed = _staged.OfType<ShipmentCreationFailedEvent>().Single();
        (failed.OrderId, failed.Reason).Should().Be((orderId, "technical failure after 5 attempts: boom"));
    }

    [Fact]
    public void The_failure_event_carries_a_tenant_like_every_event_that_leaves_the_service()
    {
        typeof(ShipmentCreationFailedEvent).GetInterfaces().Should().Contain(typeof(ERP.Shared.Core.Domain.ITenantEvent));
    }
}
