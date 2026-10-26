using ERP.SHP.Application.Interfaces;
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

        public SagaShipmentRequestProcessor(IShippingUnitOfWork uow, IMediator mediator, ILogger<SagaShipmentRequestProcessor> logger)
        {
            _uow = uow;
            _mediator = mediator;
            _logger = logger;
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

            // The tenant's own default carrier (first active one by name).
            var carrier = (await _uow.Carriers.GetActiveAsync(ct)).OrderBy(c => c.Name).FirstOrDefault();
            if (carrier == null)
            {
                await FailAsync(command.OrderId, "there is no active carrier", ct);
                return SagaShipmentOutcome.Failed;
            }

            await _mediator.Send(command.ToCreateShipmentCommand(carrier.Id), ct);
            return SagaShipmentOutcome.Created;
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
