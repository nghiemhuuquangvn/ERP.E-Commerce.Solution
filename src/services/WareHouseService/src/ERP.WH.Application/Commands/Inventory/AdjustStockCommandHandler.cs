using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Inventory;

public class RequestStockAdjustmentCommandHandler : IRequestHandler<RequestStockAdjustmentCommand, StockAdjustmentDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public RequestStockAdjustmentCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<StockAdjustmentDto> Handle(RequestStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found");

        var adjustment = new StockAdjustment(request.ProductId, request.QuantityDelta, request.Reason, request.RequestedBy, request.LocationId, request.Notes);
        await _uow.StockAdjustments.AddAsync(adjustment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Map(adjustment);
    }

    internal static StockAdjustmentDto Map(StockAdjustment a) => new()
    {
        Id = a.Id,
        ProductId = a.ProductId,
        LocationId = a.LocationId,
        QuantityDelta = a.QuantityDelta,
        Reason = a.Reason.ToString(),
        Notes = a.Notes,
        Status = a.Status.ToString(),
        RequestedBy = a.RequestedBy,
        RequestedAt = a.RequestedAt,
        ApprovedBy = a.ApprovedBy,
        ApprovedAt = a.ApprovedAt,
        RejectionReason = a.RejectionReason
    };
}

/// <summary>
/// The ONLY place a stock adjustment actually reaches Inventory — moved here, almost
/// unchanged, from what used to be AdjustStockCommandHandler's direct-write body. Business
/// rule: an adjustment must be approved before it can affect Inventory; RequestStock
/// AdjustmentCommandHandler above never touches Inventory at all.
/// </summary>
public class ApproveStockAdjustmentCommandHandler : IRequestHandler<ApproveStockAdjustmentCommand, AdjustStockResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;

    public ApproveStockAdjustmentCommandHandler(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
    }

    public async Task<AdjustStockResponse> Handle(ApproveStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var adjustment = await _uow.StockAdjustments.GetByIdAsync(request.AdjustmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Stock adjustment {request.AdjustmentId} not found");

        var inventory = await _uow.Inventories.GetByProductIdAsync(adjustment.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {adjustment.ProductId} not found");

        adjustment.Approve(request.ApprovedBy);
        await _uow.StockAdjustments.UpdateAsync(adjustment, cancellationToken);

        var previousQuantity = inventory.TotalQuantity;

        inventory.AdjustStock(adjustment.QuantityDelta);
        await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

        if (adjustment.LocationId.HasValue)
        {
            var locationStock = await _uow.InventoryLocationStocks.GetAsync(adjustment.ProductId, adjustment.LocationId.Value, cancellationToken);
            if (locationStock == null)
            {
                locationStock = new InventoryLocationStock(adjustment.ProductId, adjustment.LocationId.Value);
                await _uow.InventoryLocationStocks.AddAsync(locationStock, cancellationToken);
            }
            locationStock.AdjustStock(adjustment.QuantityDelta);
            await _uow.InventoryLocationStocks.UpdateAsync(locationStock, cancellationToken);
        }

        var movementType = adjustment.Reason == AdjustmentReason.Damaged ? MovementType.Damaged : MovementType.Adjusted;
        var reasonText = string.IsNullOrWhiteSpace(adjustment.Notes)
            ? adjustment.Reason.ToString()
            : $"{adjustment.Reason}: {adjustment.Notes}";

        var movement = new StockMovement(
            adjustment.ProductId,
            adjustment.QuantityDelta,
            movementType,
            fromLocation: null,
            toLocation: null,
            reference: null,
            reason: reasonText);

        await _uow.StockMovements.AddAsync(movement, cancellationToken);

        _outboxWriter.Stage(new StockMovementRecordedEvent
        {
            MovementId = movement.MovementId,
            ProductId = adjustment.ProductId,
            Quantity = adjustment.QuantityDelta,
            MovementType = movementType.ToString(),
            Reason = reasonText,
            RecordedAt = movement.RecordedAt,
            EventCreatedAt = DateTime.UtcNow
        });

        _outboxWriter.Stage(new InventoryUpdatedEvent
        {
            ProductId = adjustment.ProductId,
            PreviousQuantity = previousQuantity,
            CurrentQuantity = inventory.TotalQuantity,
            MovementType = movementType.ToString(),
            UpdatedAt = DateTime.UtcNow,
            EventCreatedAt = DateTime.UtcNow
        });

        _outboxWriter.Stage(new StockAdjustmentApprovedEvent
        {
            AdjustmentId = adjustment.Id,
            ProductId = adjustment.ProductId,
            QuantityDelta = adjustment.QuantityDelta,
            ApprovedBy = request.ApprovedBy,
            ApprovedAt = adjustment.ApprovedAt!.Value,
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

public class RejectStockAdjustmentCommandHandler : IRequestHandler<RejectStockAdjustmentCommand, StockAdjustmentDto>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;

    public RejectStockAdjustmentCommandHandler(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
    }

    public async Task<StockAdjustmentDto> Handle(RejectStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var adjustment = await _uow.StockAdjustments.GetByIdAsync(request.AdjustmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Stock adjustment {request.AdjustmentId} not found");

        adjustment.Reject(request.RejectedBy, request.Reason);
        await _uow.StockAdjustments.UpdateAsync(adjustment, cancellationToken);

        _outboxWriter.Stage(new StockAdjustmentRejectedEvent
        {
            AdjustmentId = adjustment.Id,
            ProductId = adjustment.ProductId,
            RejectedBy = request.RejectedBy,
            Reason = request.Reason,
            RejectedAt = adjustment.ApprovedAt!.Value,
            EventCreatedAt = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync(cancellationToken);

        return RequestStockAdjustmentCommandHandler.Map(adjustment);
    }
}
