using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>
/// First-class stock adjustment workflow (broken/lost goods, cycle-count corrections)
/// with a reason code, separate from the generic RecordStockMovementCommand which
/// only accepted a free-text reason string.
/// </summary>
public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, AdjustStockResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventPublisher _eventPublisher;

    public AdjustStockCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventPublisher eventPublisher)
    {
        _uow = uow;
        _eventPublisher = eventPublisher;
    }

    public async Task<AdjustStockResponse> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        if (request.QuantityDelta == 0)
            throw new InvalidOperationException("Adjustment quantity delta must not be zero");

        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
            throw new InvalidOperationException($"Product {request.ProductId} not found");

        var previousQuantity = inventory.TotalQuantity;

        inventory.AdjustStock(request.QuantityDelta);
        await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

        var movementType = request.Reason == AdjustmentReason.Damaged ? MovementType.Damaged : MovementType.Adjusted;
        var reasonText = string.IsNullOrWhiteSpace(request.Notes)
            ? request.Reason.ToString()
            : $"{request.Reason}: {request.Notes}";

        var movement = new StockMovement(
            request.ProductId,
            request.QuantityDelta,
            movementType,
            fromLocation: null,
            toLocation: null,
            reference: null,
            reason: reasonText);

        await _uow.StockMovements.AddAsync(movement, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(new StockMovementRecordedEvent
        {
            MovementId = movement.MovementId,
            ProductId = request.ProductId,
            Quantity = request.QuantityDelta,
            MovementType = movementType.ToString(),
            Reason = reasonText,
            RecordedAt = movement.RecordedAt,
            EventCreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _eventPublisher.PublishAsync(new InventoryUpdatedEvent
        {
            ProductId = request.ProductId,
            PreviousQuantity = previousQuantity,
            CurrentQuantity = inventory.TotalQuantity,
            MovementType = movementType.ToString(),
            UpdatedAt = DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        }, cancellationToken);

        if (inventory.IsLowStock())
        {
            await _eventPublisher.PublishAsync(new StockAlertEvent
            {
                ProductId = inventory.ProductId,
                ProductName = inventory.ProductName,
                CurrentQuantity = inventory.TotalQuantity,
                ReorderPoint = inventory.ReorderPoint,
                ReorderQuantity = inventory.ReorderQuantity,
                EventCreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return new AdjustStockResponse
        {
            ProductId = inventory.ProductId,
            PreviousQuantity = previousQuantity,
            CurrentQuantity = inventory.TotalQuantity,
            MovementId = movement.MovementId,
            AdjustedAt = movement.RecordedAt
        };
    }
}
