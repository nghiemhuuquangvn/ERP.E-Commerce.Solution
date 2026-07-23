using MediatR;
using ERP.WH.Application.Commands.Inventory;
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

    public SalesOrderShippedEventHandler(IReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task Handle(SalesOrderShippedEvent @event, CancellationToken cancellationToken)
    {
        var reservations = await _reservationRepository.GetByOrderIdAsync(@event.OrderId, cancellationToken);

        foreach (var reservation in reservations)
        {
            if (reservation.Status == ReservationStatus.Reserved)
            {
                reservation.Fulfill();
                await _reservationRepository.UpdateAsync(reservation, cancellationToken);
            }
        }
    }
}
