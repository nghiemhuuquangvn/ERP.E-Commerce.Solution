using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.EventHandlers;

public class SalesOrderShippedEvent : INotification
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public required string TrackingNumber { get; set; }
    public DateTime ShippedAt { get; set; }
    public required string CorrelationId { get; set; }
}

public class SalesOrderShippedEventHandler : INotificationHandler<SalesOrderShippedEvent>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationFulfillmentService _fulfillmentService;

    public SalesOrderShippedEventHandler(IReservationRepository reservationRepository, IReservationFulfillmentService fulfillmentService)
    {
        _reservationRepository = reservationRepository;
        _fulfillmentService = fulfillmentService;
    }

    public async Task Handle(SalesOrderShippedEvent @event, CancellationToken cancellationToken)
    {
        var reservations = await _reservationRepository.GetByOrderIdAsync(@event.OrderId, cancellationToken);

        foreach (var reservation in reservations)
        {
            if (reservation.Status == ReservationStatus.Reserved)
            {
                // Previously this only flipped Status to Fulfilled — on-hand quantity/lot
                // stock was never actually reduced through the normal reserve-then-ship
                // flow. ConsumeAsync now does the real stock reduction + COGS logging and
                // marks the reservation Fulfilled itself (single save per reservation).
                await _fulfillmentService.ConsumeAsync(reservation, cancellationToken);
            }
        }
    }
}
