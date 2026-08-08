using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Services;

/// <summary>
/// The real ship-time stock reduction — previously SalesOrderShippedEventHandler only
/// flipped the Reservation to Fulfilled and never touched Inventory/InventoryLot at all,
/// so on-hand quantity silently never decreased through the normal reserve-then-ship
/// flow. This service consumes exactly the lot allocations ReserveInventoryCommandHandler
/// already recorded (soft-held, not destructively depleted) — no re-picking, no double
/// depletion — and writes a CostConsumptionLog row per lot so Finance/audit has a real
/// per-sale COGS trail.
/// </summary>
public interface IReservationFulfillmentService
{
    Task ConsumeAsync(Reservation reservation, CancellationToken cancellationToken);
}

public class ReservationFulfillmentService : IReservationFulfillmentService
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;

    public ReservationFulfillmentService(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter, ICacheService cache)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
    }

    public async Task ConsumeAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        foreach (var item in reservation.ReservedItems)
        {
            var inventory = await _uow.Inventories.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (inventory == null)
                continue;

            var previousQuantity = inventory.TotalQuantity;
            inventory.SellStock(item.Quantity);
            await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

            if (Guid.TryParse(item.WarehouseLocation, out var locationId))
            {
                var locationStock = await _uow.InventoryLocationStocks.GetAsync(item.ProductId, locationId, cancellationToken);
                if (locationStock != null)
                {
                    locationStock.SellStock(item.Quantity);
                    await _uow.InventoryLocationStocks.UpdateAsync(locationStock, cancellationToken);
                }
            }

            if (item.LotAllocations.Count > 0)
            {
                foreach (var allocation in item.LotAllocations)
                {
                    var lot = await _uow.InventoryLots.GetByIdAsync(allocation.LotId, cancellationToken);
                    if (lot == null)
                        continue;

                    lot.Consume(allocation.Quantity);
                    await _uow.InventoryLots.UpdateAsync(lot, cancellationToken);

                    var log = new CostConsumptionLog(item.ProductId, lot.LotId, allocation.Quantity, lot.UnitCost, reservation.OrderId);
                    await _uow.CostConsumptionLogs.AddAsync(log, cancellationToken);
                }
            }
            else
            {
                // Non-lot-tracked product (or Average valuation method) — no per-lot
                // breakdown exists, so log one row against the product's current average cost.
                var log = new CostConsumptionLog(item.ProductId, lotId: null, item.Quantity, inventory.Cost, reservation.OrderId);
                await _uow.CostConsumptionLogs.AddAsync(log, cancellationToken);
            }

            var movement = new StockMovement(
                Guid.Parse("00000000-0000-0000-0000-000000000001"), // see RecordStockMovementCommandHandler
                item.ProductId,
                item.Quantity,
                MovementType.Sold,
                fromLocation: item.WarehouseLocation,
                toLocation: null,
                reference: null,
                reason: "Order shipped",
                sourceOrderId: reservation.OrderId);

            await _uow.StockMovements.AddAsync(movement, cancellationToken);

            _outboxWriter.Stage(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                MovementType = nameof(MovementType.Sold),
                FromLocation = item.WarehouseLocation,
                SourceOrderId = reservation.OrderId,
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            });

            _outboxWriter.Stage(new InventoryUpdatedEvent
            {
                ProductId = item.ProductId,
                PreviousQuantity = previousQuantity,
                CurrentQuantity = inventory.TotalQuantity,
                MovementType = nameof(MovementType.Sold),
                ReferenceId = reservation.OrderId,
                UpdatedAt = DateTime.UtcNow,
                EventCreatedAt = DateTime.UtcNow
            });

            if (inventory.IsLowStock())
            {
                _outboxWriter.Stage(new StockAlertEvent
                {
                    ProductId = inventory.ProductId,
                    ProductName = inventory.ProductName,
                    CurrentQuantity = inventory.TotalQuantity,
                    ReorderPoint = inventory.ReorderPoint,
                    ReorderQuantity = inventory.ReorderQuantity,
                    EventCreatedAt = DateTime.UtcNow
                });
            }

            await _cache.RemoveAsync($"wh:inventory:{item.ProductId}", cancellationToken);
        }

        reservation.Fulfill();
        await _uow.Reservations.UpdateAsync(reservation, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

public interface ICostConsumptionLogRepository
{
    Task AddAsync(CostConsumptionLog log, CancellationToken cancellationToken);
    Task<List<CostConsumptionLog>> GetByProductIdAsync(Guid productId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
    Task<List<CostConsumptionLog>> GetBySourceOrderIdAsync(Guid sourceOrderId, CancellationToken cancellationToken);
}
