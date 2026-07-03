using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Stocktakes;

public class CompleteStocktakeCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
}

/// <summary>
/// Posts every counted line whose Variance != 0 as an Adjusted stock movement so the
/// physical count becomes the new system quantity — this is the step that actually
/// reconciles "kiểm kê vật lý" against real inventory, not just a report.
/// </summary>
public class CompleteStocktakeCommandHandler : IRequestHandler<CompleteStocktakeCommand, StocktakeDto>
{
    private readonly IStocktakeRepository _stocktakeRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IEventPublisher _eventPublisher;

    public CompleteStocktakeCommandHandler(
        IStocktakeRepository stocktakeRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository movementRepository,
        IEventPublisher eventPublisher)
    {
        _stocktakeRepository = stocktakeRepository;
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<StocktakeDto> Handle(CompleteStocktakeCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _stocktakeRepository.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        var variances = stocktake.Complete();
        await _stocktakeRepository.UpdateAsync(stocktake, cancellationToken);

        foreach (var line in variances)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(line.ProductId, cancellationToken);
            if (inventory == null)
                continue;

            var previousQuantity = inventory.TotalQuantity;
            inventory.AdjustStock(line.Variance);
            await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

            var movement = new StockMovement(
                line.ProductId,
                line.Variance,
                MovementType.Adjusted,
                fromLocation: null,
                toLocation: null,
                reference: stocktake.StocktakeNumber,
                reason: "Stocktake variance");

            await _movementRepository.AddAsync(movement, cancellationToken);

            await _eventPublisher.PublishAsync(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = line.ProductId,
                Quantity = line.Variance,
                MovementType = nameof(MovementType.Adjusted),
                Reference = stocktake.StocktakeNumber,
                Reason = "Stocktake variance",
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _eventPublisher.PublishAsync(new InventoryUpdatedEvent
            {
                ProductId = line.ProductId,
                PreviousQuantity = previousQuantity,
                CurrentQuantity = inventory.TotalQuantity,
                MovementType = nameof(MovementType.Adjusted),
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
        }

        await _eventPublisher.PublishAsync(new StocktakeCompletedEvent
        {
            StocktakeId = stocktake.StocktakeId,
            StocktakeNumber = stocktake.StocktakeNumber,
            LinesWithVariance = variances.Count,
            CompletedAt = stocktake.CompletedAt ?? DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}
