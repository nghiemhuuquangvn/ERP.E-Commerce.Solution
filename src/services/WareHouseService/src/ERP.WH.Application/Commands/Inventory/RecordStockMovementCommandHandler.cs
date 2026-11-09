using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Commands.Inventory;

public class RecordStockMovementCommandHandler : IRequestHandler<RecordStockMovementCommand, RecordStockMovementResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;
    private readonly IStockLedger _ledger;
    private readonly IProductionMovementService _production;

    public RecordStockMovementCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter,
        ICacheService cache,
        IStockLedger ledger,
        IProductionMovementService production)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
        _ledger = ledger;
        _production = production;
    }

    public async Task<RecordStockMovementResponse> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MovementType>(request.MovementType, ignoreCase: true, out var movementType) || !Enum.IsDefined(movementType))
            throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_MOVEMENT_TYPE", $"Unknown movement type: {request.MovementType}");

        if (movementType.IsProduction())
            return await HandleProductionAsync(request, movementType, cancellationToken);

        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
            throw new ERP.Shared.Infrastructure.Exceptions.EntityNotFoundException("Inventory", request.ProductId);

        var previousQuantity = inventory.TotalQuantity;

        var target = await _ledger.TargetAsync(null, null, cancellationToken);
        switch (movementType)
        {
            case MovementType.Received:
                await _ledger.ReceiveAsync(inventory, request.Quantity, 0, target, null, cancellationToken);
                break;
            case MovementType.Sold:
                // cost layers: under Average costing the lots are not depleted; FIFO / LIFO deplete them in that order
                var depletion = inventory.ValuationMethod == ValuationMethod.Average ? LotDepletion.None
                    : inventory.ValuationMethod == ValuationMethod.LIFO ? LotDepletion.Lifo : LotDepletion.Fifo;
                await _ledger.IssueAsync(inventory, request.Quantity, target, IssuePolicy.Refuse, depletion, cancellationToken);
                break;
            case MovementType.Adjusted:
                await _ledger.AdjustAsync(inventory, request.Quantity, target, cancellationToken);
                break;
            case MovementType.Damaged:
                await _ledger.AdjustAsync(inventory, -request.Quantity, target, cancellationToken);
                break;
            default:
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_MOVEMENT_TYPE", $"{request.MovementType} cannot be recorded as a manual movement.");
        }

        await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

        var movement = new StockMovement(
            // Empty: WarehouseDbContext.EnforceTenantOnSave stamps the tenant of the current
            // request / TenantScope (it used to be hard-coded to the SYSTEM tenant).
            Guid.Empty,
            request.ProductId,
            request.Quantity,
            movementType,
            request.FromLocation,
            request.ToLocation,
            request.Reference,
            request.Reason,
            request.SourceOrderId);
        var legacyValue = Math.Round(movementType switch
        {
            MovementType.Sold => -request.Quantity * inventory.Cost,
            MovementType.Adjusted => request.Quantity * inventory.Cost,
            MovementType.Damaged => -request.Quantity * inventory.Cost,
            _ => 0m,
        }, 2);
        movement.Describe(target.WarehouseId, null, inventory.Cost, legacyValue, inventory.TotalQuantity - previousQuantity);

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
            UnitCost = inventory.Cost,
            // a manual receipt carries no price (ReceiveStock(qty, 0) above), so it adds no value; the others move at the current cost
            ValueDelta = Math.Round(movementType switch
            {
                MovementType.Sold => -request.Quantity * inventory.Cost,
                MovementType.Adjusted => request.Quantity * inventory.Cost,
                MovementType.Damaged => -request.Quantity * inventory.Cost,
                _ => 0m,
            }, 2),
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

        if (inventory.EvaluateLowStockAlert(DateTime.UtcNow) is { } stockAlert)
            _outboxWriter.Stage(stockAlert);

        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"wh:inventory:{request.ProductId}", cancellationToken);

        return new RecordStockMovementResponse
        {
            MovementId = movement.MovementId,
            MovementType = request.MovementType,
            Quantity = request.Quantity,
            RecordedAt = DateTime.UtcNow
        };
    }

    private async Task<RecordStockMovementResponse> HandleProductionAsync(RecordStockMovementCommand request, MovementType movementType, CancellationToken ct)
    {
        LotStatus? initial = null;
        if (!string.IsNullOrWhiteSpace(request.InitialLotStatus))
        {
            if (!Enum.TryParse<LotStatus>(request.InitialLotStatus, ignoreCase: true, out var parsed))
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_LOT_STATUS", $"Unknown lot status: {request.InitialLotStatus}");
            initial = parsed;
        }

        var result = await _production.RecordAsync(new ProductionMovementRequest(
            movementType, request.ProductId, request.Quantity, request.Reference ?? string.Empty,
            request.WarehouseId, request.ToWarehouseId, request.LotId, request.LotNumber, request.ExpiryDate, request.ManufactureDate,
            request.UnitCost, initial, request.AllowNegative, request.Reason, request.SourceOrderId), ct);

        var movement = result.Movement;
        if (!result.Replayed)
        {
            _outboxWriter.Stage(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = request.ProductId,
                Quantity = movement.Quantity,
                MovementType = movementType.ToString(),
                Reference = movement.Reference,
                Reason = movement.Reason,
                SourceOrderId = movement.SourceOrderId,
                UnitCost = result.UnitCost,
                ValueDelta = result.ValueDelta,
                WarehouseId = result.WarehouseId,
                LotId = result.LotId,
                StockDelta = result.StockDelta,
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            });

            if (result.StockDelta != 0)
                _outboxWriter.Stage(new InventoryUpdatedEvent
                {
                    ProductId = request.ProductId,
                    PreviousQuantity = result.PreviousQuantity,
                    CurrentQuantity = result.CurrentQuantity,
                    MovementType = movementType.ToString(),
                    ReferenceId = request.SourceOrderId,
                    UpdatedAt = DateTime.UtcNow,
                    EventCreatedAt = DateTime.UtcNow
                });

            var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, ct);
            if (inventory?.EvaluateLowStockAlert(DateTime.UtcNow) is { } stockAlert)
                _outboxWriter.Stage(stockAlert);

            await _uow.SaveChangesAsync(ct);
            await _cache.RemoveAsync($"wh:inventory:{request.ProductId}", ct);
        }

        return new RecordStockMovementResponse
        {
            MovementId = movement.MovementId,
            MovementType = movementType.ToString(),
            Quantity = movement.Quantity,
            RecordedAt = movement.RecordedAt,
            UnitCost = result.UnitCost,
            ValueDelta = result.ValueDelta,
            WarehouseId = result.WarehouseId,
            LotId = result.LotId,
            Replayed = result.Replayed
        };
    }
}

public interface IStockMovementRepository
{
    Task<StockMovement?> GetByIdAsync(Guid movementId, CancellationToken cancellationToken);
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken);
    Task<List<StockMovement>> GetByProductIdAsync(Guid productId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid productId, string? reference, MovementType movementType, CancellationToken cancellationToken);

    /// <summary>The production movement already recorded for this document line (the idempotency key), or null.</summary>
    Task<StockMovement?> FindByKeyAsync(Guid productId, string reference, MovementType movementType, CancellationToken cancellationToken);
}
