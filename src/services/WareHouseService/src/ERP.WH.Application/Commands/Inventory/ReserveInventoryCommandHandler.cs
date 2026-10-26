using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.Shared.Infrastructure.Caching;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.Application.Commands.Inventory;

public class ReserveInventoryCommandHandler : IRequestHandler<ReserveInventoryCommand, ReserveInventoryResponse?>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;

    public ReserveInventoryCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter,
        ICacheService cache)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
    }

    public async Task<ReserveInventoryResponse?> Handle(ReserveInventoryCommand request, CancellationToken cancellationToken)
    {
        // Idempotency: a redelivered ReserveInventoryCommand for an order that already
        // has an active reservation must not double-reserve stock. Matches the same
        // terminal/active-state short-circuit idiom SalesOrderCancelledEventHandler/
        // SalesOrderShippedEventHandler already use for their own redelivery safety.
        var existingReservations = await _uow.Reservations.GetByOrderIdAsync(request.OrderId, cancellationToken);
        var activeReservation = existingReservations.FirstOrDefault(r => r.Status == ReservationStatus.Reserved);
        if (activeReservation != null)
        {
            return new ReserveInventoryResponse
            {
                ReservationId = activeReservation.ReservationId,
                OrderId = activeReservation.OrderId,
                ReservedItems = activeReservation.ReservedItems.Select(i => new ReservedItemResponse
                {
                    ProductId = i.ProductId,
                    ReservedQuantity = i.Quantity,
                    Location = i.WarehouseLocation
                }).ToList(),
                Status = "Reserved",
                ReservedAt = activeReservation.ReservedAt
            };
        }

        // Two-phase: check every line's availability first. Previously this reserved
        // items one at a time and threw on the first shortfall, leaving earlier items
        // in the same request already reserved with no rollback.
        var insufficient = new List<InsufficientItemDto>();
        var inventories = new Dictionary<Guid, InventoryEntity>();

        foreach (var item in request.Items)
        {
            var inventory = await _uow.Inventories.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (inventory == null || !inventory.CanReserve(item.Quantity))
            {
                insufficient.Add(new InsufficientItemDto
                {
                    ProductId = item.ProductId,
                    RequestedQuantity = item.Quantity,
                    AvailableQuantity = inventory?.AvailableQuantity ?? 0
                });
                continue;
            }

            inventories[item.ProductId] = inventory;
        }

        if (insufficient.Count > 0)
        {
            _outboxWriter.Stage(new InventoryInsufficientEvent
            {
                OrderId = request.OrderId,
                InsufficientItems = insufficient,
                Reason = $"{insufficient.Count} line item(s) could not be fully reserved",
                CorrelationId = request.CorrelationId,
                EventCreatedAt = DateTime.UtcNow
            });
            await _uow.SaveChangesAsync(cancellationToken);
            return null;
        }

        var reservedItems = new List<ReservedItem>();
        var reservedItemResponses = new List<ReservedItemResponse>();

        foreach (var item in request.Items)
        {
            var inventory = inventories[item.ProductId];
            inventory.ReserveStock(item.Quantity);
            await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

            var location = await ReserveAtLocationAsync(item.ProductId, item.Quantity, cancellationToken);
            var lotAllocations = await AllocateFefoLotAsync(item.ProductId, item.Quantity, cancellationToken);

            reservedItems.Add(new ReservedItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                WarehouseLocation = location,
                LotAllocations = lotAllocations
            });

            reservedItemResponses.Add(new ReservedItemResponse
            {
                ProductId = item.ProductId,
                ReservedQuantity = item.Quantity,
                Location = location
            });
        }

        var reservation = new Reservation(request.OrderId, reservedItems, request.HoldUntil);

        var eventItems = reservedItems.Select(i => new ReservedItemDto
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            WarehouseLocation = i.WarehouseLocation
        }).ToList();

        await _uow.Reservations.AddAsync(reservation, cancellationToken);

        _outboxWriter.Stage(new InventoryReservedEvent
        {
            ReservationId = reservation.ReservationId,
            OrderId = request.OrderId,
            ReservedItems = eventItems,
            ReservedAt = DateTime.UtcNow,
            CorrelationId = request.CorrelationId,
            EventCreatedAt = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync(cancellationToken);

        foreach (var productId in inventories.Keys)
            await _cache.RemoveAsync($"wh:inventory:{productId}", cancellationToken);

        return new ReserveInventoryResponse
        {
            ReservationId = reservation.ReservationId,
            OrderId = request.OrderId,
            ReservedItems = reservedItemResponses,
            Status = "Reserved",
            ReservedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// First-fit: picks the first location whose own AvailableQuantity covers the full
    /// requested quantity — does not split one line's reservation across multiple
    /// locations. Falls back to "DEFAULT_LOCATION" (previous hardcoded behavior) when the
    /// product has no location-level stock rows yet (bin-level tracking is additive; not
    /// every product has been received against a specific location).
    /// </summary>
    private async Task<string> ReserveAtLocationAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var locationStocks = await _uow.InventoryLocationStocks.GetByProductIdAsync(productId, cancellationToken);
        var candidate = locationStocks.FirstOrDefault(s => s.CanReserve(quantity));
        if (candidate == null)
            return "DEFAULT_LOCATION";

        candidate.ReserveStock(quantity);
        await _uow.InventoryLocationStocks.UpdateAsync(candidate, cancellationToken);
        return candidate.LocationId.ToString();
    }

    /// <summary>
    /// FEFO: earliest ExpiryDate first (nulls last), then earliest ReceivedDate. Places a
    /// soft hold (Reserve, not Deplete) across lots until the requested quantity is
    /// covered — RemainingQuantity is untouched here; real depletion happens later, at
    /// ship time, via IReservationFulfillmentService consuming exactly these allocations.
    /// Returns an empty list for non-lot-tracked products (no lots exist).
    /// </summary>
    private async Task<List<LotAllocation>> AllocateFefoLotAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var lots = await _uow.InventoryLots.GetByProductIdAsync(productId, includeDepleted: false, cancellationToken);
        var ordered = lots
            .OrderBy(l => l.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(l => l.ReceivedDate)
            .ToList();

        var allocations = new List<LotAllocation>();
        var remaining = quantity;

        foreach (var lot in ordered)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(lot.AvailableQuantity, remaining);
            if (take <= 0)
                continue;

            lot.Reserve(take);
            await _uow.InventoryLots.UpdateAsync(lot, cancellationToken);
            allocations.Add(new LotAllocation { LotId = lot.LotId, Quantity = take });
            remaining -= take;
        }

        return allocations;
    }
}

public interface IInventoryRepository
{
    Task<InventoryEntity?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<InventoryEntity?> GetByProductSkuAsync(string sku, CancellationToken cancellationToken);
    Task UpdateAsync(InventoryEntity inventory, CancellationToken cancellationToken);
    Task AddAsync(InventoryEntity inventory, CancellationToken cancellationToken);
    Task<List<InventoryEntity>> GetAllAsync(CancellationToken cancellationToken);
    /// <summary>Tenants that have stock rows — across tenants, for the replenishment scan.</summary>
    Task<List<Guid>> GetTenantsWithInventoryAsync(CancellationToken cancellationToken);
}

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<List<Reservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);
    Task<List<Reservation>> GetExpiredAsync(DateTime asOfUtc, CancellationToken cancellationToken);
    /// <summary>Tenants that have expired reservations — across tenants, for the expiry job.</summary>
    Task<List<Guid>> GetTenantsWithExpiredAsync(DateTime asOfUtc, CancellationToken cancellationToken);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken);
}
