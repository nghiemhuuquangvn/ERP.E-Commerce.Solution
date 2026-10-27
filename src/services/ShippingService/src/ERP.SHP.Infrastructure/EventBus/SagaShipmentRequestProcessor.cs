using ERP.SHP.Application.Commands;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.EventBus
{
    public enum SagaShipmentOutcome
    {
        /// <summary>A shipment was created and ShipmentCreated will follow through the outbox.</summary>
        Created,

        /// <summary>The order already has a shipment: nothing was created, ShipmentCreated is announced again for the saga's retry.</summary>
        AlreadyExists,

        /// <summary>The shipment cannot be created: ShipmentCreationFailed was staged, the saga compensates.</summary>
        Failed,
    }

    /// <summary>
    /// Handles the order saga's "create the shipment" command (ROADMAP P1-5). It never creates a second shipment for an order (the saga publishes the command again when it
    /// gets no answer), and a command that cannot be carried out (no delivery address, no active carrier) is answered with <see cref="ShipmentCreationFailedEvent"/>
    /// instead of being dropped silently. The caller runs inside the command's tenant scope.
    /// </summary>
    public class SagaShipmentRequestProcessor
    {
        private readonly IShippingUnitOfWork _uow;
        private readonly IMediator _mediator;
        private readonly ILogger<SagaShipmentRequestProcessor> _logger;
        private readonly IProductWeightClient _weights;

        public SagaShipmentRequestProcessor(IShippingUnitOfWork uow, IMediator mediator, ILogger<SagaShipmentRequestProcessor> logger, IProductWeightClient weights)
        {
            _uow = uow;
            _mediator = mediator;
            _logger = logger;
            _weights = weights;
        }

        public async Task<SagaShipmentOutcome> ProcessAsync(SagaCreateShipmentCommand command, CancellationToken ct = default)
        {
            // A shipment of the order exists already (the saga's retry, or a redelivery): say so again, create nothing.
            var existing = (await _uow.Shipments.GetByOrderIdAsync(command.OrderId, ct))
                .FirstOrDefault(s => s.Status != ShipmentStatus.Cancelled);
            if (existing != null)
            {
                _logger.LogInformation("Order {OrderId} already has shipment {ShipmentId}: announcing it again", command.OrderId, existing.Id);
                await _uow.StageEventAsync(new ShipmentCreatedEvent(existing.Id, existing.OrderId, existing.CarrierId, existing.CarrierName), ct);
                return SagaShipmentOutcome.AlreadyExists;
            }

            // Never create a shipment the carrier can't deliver.
            if (!command.HasDeliveryAddress)
            {
                await FailAsync(command.OrderId, "the order has no delivery address", ct);
                return SagaShipmentOutcome.Failed;
            }

            // The carrier is chosen while booking: the shop's rules, then the cheapest quote, the next one when a carrier refuses.
            try
            {
                var create = command.ToCreateShipmentCommand();
                await ApplyProductWeightsAsync(create, ct);
                await _mediator.Send(create, ct);
                return SagaShipmentOutcome.Created;
            }
            catch (CarrierBookingException ex) when (!ex.IsTransient)
            {
                // No carrier accepted the parcel (or none is set up): the order cannot ship, the saga compensates and keeps the reason.
                await FailAsync(command.OrderId, ex.Message, ct);
                return SagaShipmentOutcome.Failed;
            }
            // A carrier that could not be reached is not a verdict: the exception goes to the consumer, which tries again before giving up.
        }

        /// <summary>The products' own weights (kg per unit) go on the lines; a product without one keeps 0 and the booking uses the shop's default item weight.</summary>
        private async Task ApplyProductWeightsAsync(CreateShipmentCommand create, CancellationToken ct)
        {
            var weights = await _weights.GetWeightsAsync(create.Items.Select(i => i.ProductId).Distinct().ToList(), ct);
            foreach (var item in create.Items)
                if (item.Weight <= 0 && weights.TryGetValue(item.ProductId, out var grams))
                    item.Weight = grams / 1000m;
        }

        /// <summary>Tells Sales the shipment of the order could not be created.</summary>
        public Task ReportFailureAsync(Guid orderId, string reason, CancellationToken ct = default) => FailAsync(orderId, reason, ct);

        private async Task FailAsync(Guid orderId, string reason, CancellationToken ct)
        {
            _logger.LogWarning("Shipment for order {OrderId} not created: {Reason}", orderId, reason);
            await _uow.StageEventAsync(new ShipmentCreationFailedEvent(orderId, reason), ct);
        }
    }
}
