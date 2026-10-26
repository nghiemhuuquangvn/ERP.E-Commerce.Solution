using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Commands.Stocktakes;

public class CompleteStocktakeCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
}

/// <summary>
/// Locks counting and computes variances — does NOT touch Inventory anymore. Business
/// rule: a stocktake's variance must be approved (ApproveStocktakeCommand) before it
/// posts, same as StockAdjustment. Previously this handler looped every variance and
/// wrote straight to Inventory in the same step as "completing" the count.
/// </summary>
public class CompleteStocktakeCommandHandler : IRequestHandler<CompleteStocktakeCommand, StocktakeDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CompleteStocktakeCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<StocktakeDto> Handle(CompleteStocktakeCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _uow.Stocktakes.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        stocktake.Complete();
        await _uow.Stocktakes.UpdateAsync(stocktake, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}

public class ApproveStocktakeCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
}

/// <summary>The only step that actually posts stocktake variance to Inventory — moved here, almost unchanged, from the old CompleteStocktakeCommandHandler's loop.</summary>
public class ApproveStocktakeCommandHandler : IRequestHandler<ApproveStocktakeCommand, StocktakeDto>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;

    public ApproveStocktakeCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter,
        ICacheService cache)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
    }

    public async Task<StocktakeDto> Handle(ApproveStocktakeCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _uow.Stocktakes.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        var variances = stocktake.Approve();
        await _uow.Stocktakes.UpdateAsync(stocktake, cancellationToken);

        foreach (var line in variances)
        {
            var inventory = await _uow.Inventories.GetByProductIdAsync(line.ProductId, cancellationToken);
            if (inventory == null)
                continue;

            var previousQuantity = inventory.TotalQuantity;
            inventory.AdjustStock(line.Variance);
            await _uow.Inventories.UpdateAsync(inventory, cancellationToken);
            await _cache.RemoveAsync($"wh:inventory:{line.ProductId}", cancellationToken);

            if (stocktake.LocationId.HasValue)
            {
                var locationStock = await _uow.InventoryLocationStocks.GetAsync(line.ProductId, stocktake.LocationId.Value, cancellationToken);
                if (locationStock == null)
                {
                    locationStock = new InventoryLocationStock(line.ProductId, stocktake.LocationId.Value);
                    await _uow.InventoryLocationStocks.AddAsync(locationStock, cancellationToken);
                }
                locationStock.AdjustStock(line.Variance);
                await _uow.InventoryLocationStocks.UpdateAsync(locationStock, cancellationToken);
            }

            var movement = new StockMovement(
                Guid.Empty, // stamped with the current tenant on save (see RecordStockMovementCommandHandler)
                line.ProductId,
                line.Variance,
                MovementType.Adjusted,
                fromLocation: null,
                toLocation: null,
                reference: stocktake.StocktakeNumber,
                reason: line.VarianceReason ?? "Stocktake variance");

            await _uow.StockMovements.AddAsync(movement, cancellationToken);

            _outboxWriter.Stage(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = line.ProductId,
                Quantity = line.Variance,
                MovementType = nameof(MovementType.Adjusted),
                Reference = stocktake.StocktakeNumber,
                Reason = movement.Reason,
                UnitCost = inventory.Cost,
                ValueDelta = Math.Round(line.Variance * inventory.Cost, 2),
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            });

            _outboxWriter.Stage(new InventoryUpdatedEvent
            {
                ProductId = line.ProductId,
                PreviousQuantity = previousQuantity,
                CurrentQuantity = inventory.TotalQuantity,
                MovementType = nameof(MovementType.Adjusted),
                UpdatedAt = DateTime.UtcNow,
                EventCreatedAt = DateTime.UtcNow
            });

            if (inventory.EvaluateLowStockAlert(DateTime.UtcNow) is { } stockAlert)
                _outboxWriter.Stage(stockAlert);
        }

        _outboxWriter.Stage(new StocktakeCompletedEvent
        {
            StocktakeId = stocktake.StocktakeId,
            StocktakeNumber = stocktake.StocktakeNumber,
            LinesWithVariance = variances.Count,
            CompletedAt = stocktake.CompletedAt ?? DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync(cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}
