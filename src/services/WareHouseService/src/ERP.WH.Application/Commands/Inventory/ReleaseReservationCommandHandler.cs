using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, ReleaseReservationResponse>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public ReleaseReservationCommandHandler(
        IReservationRepository reservationRepository,
        IInventoryRepository inventoryRepository)
    {
        _reservationRepository = reservationRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<ReleaseReservationResponse> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken);
        if (reservation == null)
            throw new InvalidOperationException($"Reservation {request.ReservationId} not found");

        foreach (var item in reservation.ReservedItems)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (inventory != null)
            {
                inventory.ReleaseReservation(item.Quantity);
                await _inventoryRepository.UpdateAsync(inventory, cancellationToken);
            }
        }

        reservation.Release(request.Reason);
        await _reservationRepository.UpdateAsync(reservation, cancellationToken);

        return new ReleaseReservationResponse
        {
            ReservationId = reservation.ReservationId,
            Status = "Released",
            ReleasedAt = DateTime.UtcNow
        };
    }
}
