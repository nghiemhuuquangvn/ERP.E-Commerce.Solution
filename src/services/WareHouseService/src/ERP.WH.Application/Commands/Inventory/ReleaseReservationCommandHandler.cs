using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Commands.Inventory;

public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, ReleaseReservationResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly ICacheService _cache;

    public ReleaseReservationCommandHandler(IWarehouseUnitOfWork uow, ICacheService cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<ReleaseReservationResponse> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _uow.Reservations.GetByIdAsync(request.ReservationId, cancellationToken);
        if (reservation == null)
            throw new InvalidOperationException($"Reservation {request.ReservationId} not found");

        await ReleaseAllocationsAsync(_uow, reservation, cancellationToken);

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

    /// <summary>
    /// Reverses the soft holds placed by ReserveInventoryCommandHandler (Inventory,
    /// InventoryLocationStock, InventoryLot ReservedQuantity) without touching any
    /// RemainingQuantity/TotalQuantity — shared by this handler (manual/saga cancel) and
    /// ReservationExpiryProcessor (auto-expiry), which only differ in what status they set
    /// on the reservation afterward (Released vs Expired).
    /// </summary>
    public static async Task ReleaseAllocationsAsync(IWarehouseUnitOfWork uow, Reservation reservation, CancellationToken cancellationToken)
    {
        foreach (var item in reservation.ReservedItems)
        {
            var inventory = await uow.Inventories.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (inventory != null)
            {
                inventory.ReleaseReservation(item.Quantity);
                await uow.Inventories.UpdateAsync(inventory, cancellationToken);
            }

            if (Guid.TryParse(item.WarehouseLocation, out var locationId))
            {
                var locationStock = await uow.InventoryLocationStocks.GetAsync(item.ProductId, locationId, cancellationToken);
                if (locationStock != null)
                {
                    locationStock.ReleaseReservation(item.Quantity);
                    await uow.InventoryLocationStocks.UpdateAsync(locationStock, cancellationToken);
                }
            }

            foreach (var allocation in item.LotAllocations)
            {
                var lot = await uow.InventoryLots.GetByIdAsync(allocation.LotId, cancellationToken);
                if (lot != null)
                {
                    lot.ReleaseReservation(allocation.Quantity);
                    await uow.InventoryLots.UpdateAsync(lot, cancellationToken);
                }
            }
        }
    }
}
