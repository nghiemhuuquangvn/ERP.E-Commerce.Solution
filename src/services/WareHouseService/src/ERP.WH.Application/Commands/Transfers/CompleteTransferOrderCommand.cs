using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Transfers;

public class CompleteTransferOrderCommand : IRequest<TransferOrderDto>
{
    public Guid TransferOrderId { get; set; }
}

/// <summary>
/// Completing a transfer does not change a product's company-wide TotalQuantity
/// (Inventory here is a single global pool per product, not tracked per-location) —
/// it records the movement for audit purposes and, when this is the product's only
/// tracked location, moves its recorded "home" location forward.
/// </summary>
public class CompleteTransferOrderCommandHandler : IRequestHandler<CompleteTransferOrderCommand, TransferOrderDto>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventPublisher _eventPublisher;

    public CompleteTransferOrderCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventPublisher eventPublisher)
    {
        _uow = uow;
        _eventPublisher = eventPublisher;
    }

    public async Task<TransferOrderDto> Handle(CompleteTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _uow.TransferOrders.GetByIdAsync(request.TransferOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Transfer order {request.TransferOrderId} not found");

        order.Complete();
        await _uow.TransferOrders.UpdateAsync(order, cancellationToken);

        foreach (var line in order.Lines)
        {
            var movement = new StockMovement(
                line.ProductId,
                line.Quantity,
                MovementType.Transferred,
                fromLocation: order.FromLocationId.ToString(),
                toLocation: order.ToLocationId.ToString(),
                reference: order.TransferNumber);

            await _uow.StockMovements.AddAsync(movement, cancellationToken);

            var inventory = await _uow.Inventories.GetByProductIdAsync(line.ProductId, cancellationToken);
            if (inventory != null && (inventory.LocationId == null || inventory.LocationId == order.FromLocationId))
            {
                inventory.AssignLocation(order.ToLocationId);
                await _uow.Inventories.UpdateAsync(inventory, cancellationToken);
            }

            await _eventPublisher.PublishAsync(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                MovementType = nameof(MovementType.Transferred),
                FromLocation = movement.FromLocation,
                ToLocation = movement.ToLocation,
                Reference = order.TransferNumber,
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(new TransferOrderCompletedEvent
        {
            TransferOrderId = order.TransferOrderId,
            TransferNumber = order.TransferNumber,
            FromLocationId = order.FromLocationId,
            ToLocationId = order.ToLocationId,
            CompletedAt = order.CompletedAt ?? DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return CreateTransferOrderCommandHandler.Map(order);
    }
}
