using MediatR;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Commands.Inventory;

public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, ReleaseReservationResponse>
{
    private readonly IWarehouseUnitOfWork _uow;

    public ReleaseReservationCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ReleaseReservationResponse> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _uow.Reservations.GetByIdAsync(request.ReservationId, cancellationToken);
        if (reservation == null)
            throw new InvalidOperationException($"Reservation {request.ReservationId} not found");

        foreach (var item in reservation.ReservedItems)
        {
            var inventory = await _uow.Inventories.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (inventory != null)
            {
                inventory.ReleaseReservation(item.Quantity);
                await _uow.Inventories.UpdateAsync(inventory, cancellationToken);
            }
        }

        reservation.Release(request.Reason);
        await _uow.Reservations.UpdateAsync(reservation, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new ReleaseReservationResponse
        {
            ReservationId = reservation.ReservationId,
            Status = "Released",
            ReleasedAt = DateTime.UtcNow
        };
    }
}
