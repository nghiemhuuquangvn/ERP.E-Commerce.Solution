using MediatR;
using Microsoft.Extensions.Logging;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>
/// POS sales used to never reduce stock: POSService published TransactionCreatedEvent but
/// no Warehouse consumer existed. Mirrors ReservationFulfillmentService (stock, COGS log,
/// movement, outbox events) with two differences agreed with the owner: a sale never fails
/// for lack of stock (the goods already left the counter — stock may go negative and a
/// StockAlert is raised), and online reservations are left untouched.
/// Idempotent per (product, "POS-{tx}" / "POS-VOID-{tx}") — Kafka redelivers.
/// </summary>
public class RecordPointOfSaleStockCommandHandler : IRequestHandler<RecordPointOfSaleStockCommand, RecordPointOfSaleStockResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;
    private readonly ILogger<RecordPointOfSaleStockCommandHandler> _logger;

    public RecordPointOfSaleStockCommandHandler(
        IWarehouseUnitOfWork uow,
        IEventOutboxWriter outboxWriter,
        ICacheService cache,
        ILogger<RecordPointOfSaleStockCommandHandler> logger)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
        _logger = logger;
    }

    public static string ReferenceFor(Guid transactionId, bool isReturn) =>
        isReturn ? $"POS-VOID-{transactionId}" : $"POS-{transactionId}";

    public async Task<RecordPointOfSaleStockResponse> Handle(RecordPointOfSaleStockCommand request, CancellationToken cancellationToken)
    {
        var response = new RecordPointOfSaleStockResponse();
        var reference = ReferenceFor(request.TransactionId, request.IsReturn);
        var movementType = request.IsReturn ? MovementType.Adjusted : MovementType.Sold;

        // Same product twice in one transaction is one stock effect.
        var lines = request.Lines
            .Where(l => l.ProductId != Guid.Empty && l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .Select(g => new PointOfSaleLine { ProductId = g.Key, ProductName = g.First().ProductName, Quantity = g.Sum(l => l.Quantity) });

        foreach (var line in lines)
        {
            if (await _uow.StockMovements.ExistsAsync(line.ProductId, reference, movementType, cancellationToken))
            {
                response.LinesAlreadyApplied++;
                continue;
            }

            var inventory = await _uow.Inventories.GetByProductIdAsync(line.ProductId, cancellationToken);
            if (inventory == null)
            {
                // Never fabricate an Inventory row for a product this tenant doesn't stock.
                _logger.LogWarning("POS transaction {TransactionId}: product {ProductId} ({ProductName}) has no inventory — line skipped",
                    request.TransactionId, line.ProductId, line.ProductName);
                response.LinesWithoutInventory++;
                continue;
            }

            var previousQuantity = inventory.TotalQuantity;
            if (request.IsReturn)
            {
                inventory.AdjustStock(line.Quantity);
            }
            else
            {
                inventory.RecordPointOfSaleSale(line.Quantity);
                await _uow.CostConsumptionLogs.AddAsync(
                    new CostConsumptionLog(line.ProductId, lotId: null, line.Quantity, inventory.Cost, request.TransactionId),
                    cancellationToken);
            }
            await _uow.Inventories.UpdateAsync(inventory, cancellationToken);

            var movement = new StockMovement(
                Guid.Empty, // stamped with the current tenant on save (see RecordStockMovementCommandHandler)
                line.ProductId,
                line.Quantity,
                movementType,
                fromLocation: null,
                toLocation: null,
                reference: reference,
                reason: request.IsReturn ? "POS sale voided" : "POS sale",
                sourceOrderId: request.TransactionId);
            await _uow.StockMovements.AddAsync(movement, cancellationToken);

            _outboxWriter.Stage(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                MovementType = movementType.ToString(),
                Reference = reference,
                Reason = movement.Reason,
                SourceOrderId = request.TransactionId,
                UnitCost = inventory.Cost,
                // sale: stock value leaves at the current cost; void: it comes back at the current cost
                ValueDelta = Math.Round((request.IsReturn ? 1 : -1) * line.Quantity * inventory.Cost, 2),
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            });
            _outboxWriter.Stage(new InventoryUpdatedEvent
            {
                ProductId = line.ProductId,
                PreviousQuantity = previousQuantity,
                CurrentQuantity = inventory.TotalQuantity,
                MovementType = movementType.ToString(),
                ReferenceId = request.TransactionId,
                UpdatedAt = DateTime.UtcNow,
                EventCreatedAt = DateTime.UtcNow
            });

            // A return never raises a low-stock alert, but it still reports that stock is back above the minimum.
            var stockAlert = request.IsReturn && inventory.IsLowStock() ? null : inventory.EvaluateLowStockAlert(DateTime.UtcNow);
            if (stockAlert != null)
            {
                if (inventory.IsLowStock() && inventory.TotalQuantity < 0)
                    _logger.LogWarning("POS transaction {TransactionId}: product {ProductId} stock is negative ({Quantity}) — recount needed",
                        request.TransactionId, line.ProductId, inventory.TotalQuantity);

                _outboxWriter.Stage(stockAlert);
            }

            await _cache.RemoveAsync($"wh:inventory:{line.ProductId}", cancellationToken);
            response.LinesApplied++;
        }

        if (response.LinesApplied > 0)
            await _uow.SaveChangesAsync(cancellationToken);

        return response;
    }
}
