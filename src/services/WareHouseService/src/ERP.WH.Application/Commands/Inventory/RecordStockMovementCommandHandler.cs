using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Inventory;

public class RecordStockMovementCommandHandler : IRequestHandler<RecordStockMovementCommand, RecordStockMovementResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;

    public RecordStockMovementCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
    }

    public async Task<RecordStockMovementResponse> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken);
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
                if (inventory.ValuationMethod != ValuationMethod.Average)
                    await DepleteLotsAsync(inventory, request.Quantity, cancellationToken);
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

        await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

        var movement = new StockMovement(
            request.ProductId,
            request.Quantity,
            movementType,
            request.FromLocation,
            request.ToLocation,
            request.Reference,
            request.Reason,
            request.SourceOrderId);

        await _uow.StockMovements.AddAsync(movement, cancellationToken);

        _outboxWriter.Stage(new StockMovementRecordedEvent
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
        });

        _outboxWriter.Stage(new InventoryUpdatedEvent
        {
            ProductId = request.ProductId,
            PreviousQuantity = previousQuantity,
            CurrentQuantity = inventory.TotalQuantity,
            MovementType = request.MovementType,
            ReferenceId = request.SourceOrderId,
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

        await _uow.SaveChangesAsync(cancellationToken);

        return new RecordStockMovementResponse
        {
            MovementId = movement.MovementId,
            MovementType = request.MovementType,
            Quantity = request.Quantity,
            RecordedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Depletes cost layers so GetInventoryValuationQuery reflects which lots are
    /// actually still on hand under the product's chosen costing method. Best-effort:
    /// not all quantity may be lot-tracked (e.g. stock received before lot tracking
    /// was used), so this depletes whatever lot quantity is available up to the sold
    /// amount rather than requiring an exact match.
    /// </summary>
    private async Task DepleteLotsAsync(Domain.Aggregates.Inventory.Inventory inventory, int quantitySold, CancellationToken cancellationToken)
    {
        var lots = await _uow.InventoryLots.GetByProductIdAsync(inventory.ProductId, includeDepleted: false, cancellationToken);

        var ordered = inventory.ValuationMethod == ValuationMethod.LIFO
            ? lots.OrderByDescending(l => l.ReceivedDate).ToList()
            : lots.OrderBy(l => l.ReceivedDate).ToList();

        var remainingToDeplete = quantitySold;
        foreach (var lot in ordered)
        {
            if (remainingToDeplete <= 0)
                break;

            var depleteFromLot = Math.Min(lot.RemainingQuantity, remainingToDeplete);
            if (depleteFromLot <= 0)
                continue;

            lot.Deplete(depleteFromLot);
            await _uow.InventoryLots.UpdateAsync(lot, cancellationToken);
            remainingToDeplete -= depleteFromLot;
        }
    }
}

public interface IStockMovementRepository
{
    Task<StockMovement?> GetByIdAsync(Guid movementId, CancellationToken cancellationToken);
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken);
    Task<List<StockMovement>> GetByProductIdAsync(Guid productId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
}
