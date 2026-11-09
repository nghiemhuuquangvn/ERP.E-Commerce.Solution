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

    public RecordStockMovementCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter,
        ICacheService cache,
        IStockLedger ledger)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
        _ledger = ledger;
    }

    public async Task<RecordStockMovementResponse> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
            throw new InvalidOperationException($"Product {request.ProductId} not found");

        var previousQuantity = inventory.TotalQuantity;

        var movementType = Enum.Parse<MovementType>(request.MovementType);

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
                throw new InvalidOperationException($"Unknown movement type: {request.MovementType}");
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
}

public interface IStockMovementRepository
{
    Task<StockMovement?> GetByIdAsync(Guid movementId, CancellationToken cancellationToken);
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken);
    Task<List<StockMovement>> GetByProductIdAsync(Guid productId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid productId, string? reference, MovementType movementType, CancellationToken cancellationToken);
}
