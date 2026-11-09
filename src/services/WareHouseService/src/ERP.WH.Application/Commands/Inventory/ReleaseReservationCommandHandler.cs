using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Commands.Inventory;

public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, ReleaseReservationResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly IStockLedger _ledger;

    public ReleaseReservationCommandHandler(IWarehouseUnitOfWork uow, ICacheService cache, IStockLedger ledger)
    {
        _uow = uow;
        _cache = cache;
        _ledger = ledger;
    }

    public async Task<ReleaseReservationResponse> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _uow.Reservations.GetByIdAsync(request.ReservationId, cancellationToken);
        if (reservation == null)
            throw new ERP.Shared.Infrastructure.Exceptions.EntityNotFoundException("Reservation", request.ReservationId);

        // Idempotent: a reservation that is no longer open (released, expired, fulfilled) is answered as it is - releasing again would free stock twice.
        if (reservation.Status != ReservationStatus.Reserved)
            return new ReleaseReservationResponse
            {
                ReservationId = reservation.ReservationId,
                Status = reservation.Status.ToString(),
                ReleasedAt = reservation.ReleasedAt ?? DateTime.UtcNow
            };

        await _ledger.ReleaseAsync(reservation, cancellationToken);

        reservation.Release(request.Reason);
        await _uow.Reservations.UpdateAsync(reservation, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        foreach (var productId in reservation.ReservedItems.Select(i => i.ProductId).Distinct())
            await _cache.RemoveAsync($"wh:inventory:{productId}", cancellationToken);

        return new ReleaseReservationResponse
        {
            ReservationId = reservation.ReservationId,
            Status = "Released",
            ReleasedAt = DateTime.UtcNow
        };
    }
}
