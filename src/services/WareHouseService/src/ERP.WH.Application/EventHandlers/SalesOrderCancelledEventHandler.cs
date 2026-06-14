using MediatR;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Application.EventHandlers;

public class SalesOrderCancelledEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CancellationReason { get; set; }
    public DateTime CancelledAt { get; set; }
    public string CorrelationId { get; set; }
}

public class SalesOrderCancelledEventHandler : INotificationHandler<SalesOrderCancelledEvent>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IMediator _mediator;

    public SalesOrderCancelledEventHandler(
        IReservationRepository reservationRepository,
        IMediator mediator)
    {
        _reservationRepository = reservationRepository;
        _mediator = mediator;
    }

    public async Task Handle(SalesOrderCancelledEvent @event, CancellationToken cancellationToken)
    {
        var reservations = await _reservationRepository.GetByOrderIdAsync(@event.OrderId, cancellationToken);

        foreach (var reservation in reservations)
        {
            if (reservation.Status.ToString() == "Reserved")
            {
                var command = new ReleaseReservationCommand
                {
                    ReservationId = reservation.ReservationId,
                    Reason = @event.CancellationReason
                };

                await _mediator.Send(command, cancellationToken);
            }
        }
    }
}

public static class ReservationRepositoryExtensions
{
    public static async Task<List<IReservation>> GetByOrderIdAsync(
        this IReservationRepository repository,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return await repository.GetByOrderIdAsync(orderId, cancellationToken);
    }
}

public interface IReservation
{
    Guid ReservationId { get; }
    string Status { get; }
}
