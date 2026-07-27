using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.Shared.Infrastructure.Caching;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>
/// Real goods-receipt flow — previously the only receiving path was
/// PurchaseOrderCreatedEventHandler, which fired on PO *creation* (not actual
/// receipt) and wrote to a literal no-op IReceiptRepository stub. This command
/// is invoked directly (manual API receiving) and by the new
/// PurchaseOrderReceivedConsumer (Infrastructure/EventBus/) once goods actually
/// arrive against an approved PO.
/// </summary>
public class ReceiveStockCommandHandler : IRequestHandler<ReceiveStockCommand, ReceiveStockResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;

    public ReceiveStockCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter,
        ICacheService cache)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
    }

    public async Task<ReceiveStockResponse> Handle(ReceiveStockCommand request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            throw new InvalidOperationException("Received quantity must be positive");

        // Idempotency: a PO-driven receipt (Kafka, at-least-once delivery) must not be
        // applied twice for the same PO line. Manual API receiving has no PurchaseOrderId
        // and is intentionally not guarded here — an operator re-submitting the same
        // manual receipt is a different (and rarer) concern than broker redelivery.
        if (request.PurchaseOrderId.HasValue)
        {
            var alreadyReceived = await _uow.StockMovements.ExistsAsync(
                request.ProductId, request.Reference, MovementType.Received, cancellationToken);
            if (alreadyReceived)
            {
                var existingInventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken);
                return new ReceiveStockResponse
                {
                    ProductId = request.ProductId,
                    TotalQuantity = existingInventory?.TotalQuantity ?? 0,
                    MovementId = Guid.Empty,
                    LotId = null,
                    ReceivedAt = DateTime.UtcNow
                };
            }
        }

        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
        {
            inventory = new InventoryEntity(
                request.ProductId,
                request.ProductName ?? $"Product-{request.ProductId}",
                productSku: request.ProductSku);
            await _uow.Inventories.AddAsync(inventory, cancellationToken);
        }

        var previousQuantity = inventory.TotalQuantity;

        inventory.ReceiveStock(request.Quantity, request.UnitCost);

        if (request.LocationId.HasValue && inventory.LocationId == null)
            inventory.AssignLocation(request.LocationId);

        await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

        if (request.LocationId.HasValue)
        {
            var locationStock = await _uow.InventoryLocationStocks.GetAsync(request.ProductId, request.LocationId.Value, cancellationToken);
            if (locationStock == null)
            {
                locationStock = new InventoryLocationStock(request.ProductId, request.LocationId.Value);
                await _uow.InventoryLocationStocks.AddAsync(locationStock, cancellationToken);
            }
            locationStock.Receive(request.Quantity);
            await _uow.InventoryLocationStocks.UpdateAsync(locationStock, cancellationToken);
        }

        var movement = new StockMovement(
            request.ProductId,
            request.Quantity,
            MovementType.Received,
            fromLocation: null,
            toLocation: request.LocationId?.ToString(),
            reference: request.Reference,
            reason: null,
            sourceOrderId: request.PurchaseOrderId);

        await _uow.StockMovements.AddAsync(movement, cancellationToken);

        Guid? lotId = null;
        if (!string.IsNullOrWhiteSpace(request.LotNumber))
        {
            var lot = new InventoryLot(
                request.ProductId,
                request.LotNumber,
                request.Quantity,
                request.UnitCost,
                request.ExpiryDate,
                request.PurchaseOrderId);

            await _uow.InventoryLots.AddAsync(lot, cancellationToken);
            lotId = lot.LotId;

            if (request.SerialNumbers != null)
            {
                foreach (var serialNumber in request.SerialNumbers)
                {
                    var serial = new InventorySerial(request.ProductId, serialNumber, lotId);
                    await _uow.InventorySerials.AddAsync(serial, cancellationToken);
                }
            }
        }

        _outboxWriter.Stage(new StockMovementRecordedEvent
        {
            MovementId = movement.MovementId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            MovementType = nameof(MovementType.Received),
            FromLocation = movement.FromLocation,
            ToLocation = movement.ToLocation,
            Reason = movement.Reason,
            SourceOrderId = request.PurchaseOrderId,
            RecordedAt = movement.RecordedAt,
            EventCreatedAt = DateTime.UtcNow
        });

        _outboxWriter.Stage(new InventoryUpdatedEvent
        {
            ProductId = request.ProductId,
            PreviousQuantity = previousQuantity,
            CurrentQuantity = inventory.TotalQuantity,
            MovementType = nameof(MovementType.Received),
            ReferenceId = request.PurchaseOrderId,
            UpdatedAt = DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        });

        _outboxWriter.Stage(new ReceiptCompletedEvent
        {
            ProductId = request.ProductId,
            QuantityReceived = request.Quantity,
            LotNumber = request.LotNumber,
            PurchaseOrderId = request.PurchaseOrderId,
            Reference = request.Reference,
            ReceivedAt = movement.RecordedAt,
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

        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"wh:inventory:{request.ProductId}", cancellationToken);

        return new ReceiveStockResponse
        {
            ProductId = inventory.ProductId,
            TotalQuantity = inventory.TotalQuantity,
            MovementId = movement.MovementId,
            LotId = lotId,
            ReceivedAt = movement.RecordedAt
        };
    }
}

public interface IInventoryLotRepository
{
    Task<InventoryLot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken);
    Task<List<InventoryLot>> GetByProductIdAsync(Guid productId, bool includeDepleted, CancellationToken cancellationToken);
    Task<List<InventoryLot>> GetExpiringAsync(DateTime beforeUtc, CancellationToken cancellationToken);
    Task AddAsync(InventoryLot lot, CancellationToken cancellationToken);
    Task UpdateAsync(InventoryLot lot, CancellationToken cancellationToken);
}

public interface IInventorySerialRepository
{
    Task<InventorySerial?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken);
    Task<List<InventorySerial>> GetByProductIdAsync(Guid productId, SerialStatus? status, CancellationToken cancellationToken);
    Task AddAsync(InventorySerial serial, CancellationToken cancellationToken);
    Task UpdateAsync(InventorySerial serial, CancellationToken cancellationToken);
}
