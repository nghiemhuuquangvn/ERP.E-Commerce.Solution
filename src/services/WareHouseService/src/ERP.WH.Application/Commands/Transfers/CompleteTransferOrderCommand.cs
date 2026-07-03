using MediatR;
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
    private readonly ITransferOrderRepository _transferOrderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IEventPublisher _eventPublisher;

    public CompleteTransferOrderCommandHandler(
        ITransferOrderRepository transferOrderRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository movementRepository,
        IEventPublisher eventPublisher)
    {
        _transferOrderRepository = transferOrderRepository;
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<TransferOrderDto> Handle(CompleteTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _transferOrderRepository.GetByIdAsync(request.TransferOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Transfer order {request.TransferOrderId} not found");

        order.Complete();
        await _transferOrderRepository.UpdateAsync(order, cancellationToken);

        foreach (var line in order.Lines)
        {
            var movement = new StockMovement(
                line.ProductId,
                line.Quantity,
                MovementType.Transferred,
                fromLocation: order.FromLocationId.ToString(),
                toLocation: order.ToLocationId.ToString(),
                reference: order.TransferNumber);

            await _movementRepository.AddAsync(movement, cancellationToken);

            var inventory = await _inventoryRepository.GetByProductIdAsync(line.ProductId, cancellationToken);
            if (inventory != null && (inventory.LocationId == null || inventory.LocationId == order.FromLocationId))
            {
                inventory.AssignLocation(order.ToLocationId);
                await _inventoryRepository.UpdateAsync(inventory, cancellationToken);
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
