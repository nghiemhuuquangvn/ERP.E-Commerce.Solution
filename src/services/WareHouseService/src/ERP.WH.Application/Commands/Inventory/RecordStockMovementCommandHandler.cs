using MediatR;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Inventory;

public class RecordStockMovementCommandHandler : IRequestHandler<RecordStockMovementCommand, RecordStockMovementResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IEventPublisher _eventPublisher;

    public RecordStockMovementCommandHandler(
        IInventoryRepository inventoryRepository,
        IStockMovementRepository movementRepository,
        IEventPublisher eventPublisher)
    {
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<RecordStockMovementResponse> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
            throw new InvalidOperationException($"Product {request.ProductId} not found");

        var previousQuantity = inventory.TotalQuantity;

        var movementType = Enum.Parse<MovementType>(request.MovementType);

        switch (movementType)
        {
            case MovementType.Received:
                inventory.ReceiveStock(request.Quantity, 0);
                break;
            case MovementType.Sold:
                inventory.SellStock(request.Quantity);
                break;
            case MovementType.Adjusted:
                inventory.AdjustStock(request.Quantity);
                break;
            case MovementType.Damaged:
                inventory.AdjustStock(-request.Quantity);
                break;
            default:
                throw new InvalidOperationException($"Unknown movement type: {request.MovementType}");
        }

        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

        var movement = new StockMovement(
            request.ProductId,
            request.Quantity,
            movementType,
            request.FromLocation,
            request.ToLocation,
            request.Reference,
            request.Reason,
            request.SourceOrderId);

        await _movementRepository.AddAsync(movement, cancellationToken);

        var @event = new StockMovementRecordedEvent
        {
            MovementId = movement.MovementId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            MovementType = request.MovementType,
            FromLocation = request.FromLocation,
            ToLocation = request.ToLocation,
            Reason = request.Reason,
            SourceOrderId = request.SourceOrderId,
            RecordedAt = DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(@event, cancellationToken);

        var inventoryUpdatedEvent = new InventoryUpdatedEvent
        {
            ProductId = request.ProductId,
            PreviousQuantity = previousQuantity,
            CurrentQuantity = inventory.TotalQuantity,
            MovementType = request.MovementType,
            ReferenceId = request.SourceOrderId,
            UpdatedAt = DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(inventoryUpdatedEvent, cancellationToken);

        return new RecordStockMovementResponse
        {
            MovementId = movement.MovementId,
            MovementType = request.MovementType,
            Quantity = request.Quantity,
            RecordedAt = DateTime.UtcNow
        };
    }
}

public interface IStockMovementRepository
{
    Task<StockMovement> GetByIdAsync(Guid movementId, CancellationToken cancellationToken);
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken);
    Task<List<StockMovement>> GetByProductIdAsync(Guid productId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
}
