using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>One product of a returned order and how many came back.</summary>
public class ReturnedOrderLine
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// The goods of a returned order go back on the shelf (ROADMAP P1-5). Sales sends the whole order's shipped lines when a return is completed.
/// </summary>
public class RestockReturnedOrderCommand : IRequest<RestockReturnedOrderResult>
{
    public Guid OrderId { get; set; }
    public List<ReturnedOrderLine> Lines { get; set; } = new();
}

public class RestockReturnedOrderResult
{
    public int Restocked { get; set; }

    /// <summary>Lines already restocked by an earlier delivery of the same event.</summary>
    public int AlreadyDone { get; set; }

    /// <summary>Lines whose product is not tracked in the warehouse (nothing to put back).</summary>
    public int Skipped { get; set; }
}

/// <summary>
/// Each line is received back at the cost the goods left the warehouse at: the weighted average of the order's <see cref="CostConsumptionLog"/> rows for the product
/// (the current cost of the product when the order has none, for example an order shipped before costs were logged). It creates a lot (so FIFO issues can find it),
/// a <see cref="MovementType.Returned"/> movement with a positive value that Accounting books as Dr Inventory / Cr Cost of goods sold. Idempotent per
/// (order, product): the movement reference is <c>return:&lt;orderId&gt;</c>.
/// </summary>
public class RestockReturnedOrderCommandHandler : IRequestHandler<RestockReturnedOrderCommand, RestockReturnedOrderResult>
{
    public static string ReferenceFor(Guid orderId) => $"return:{orderId:N}";

    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;
    private readonly ILogger<RestockReturnedOrderCommandHandler> _logger;

    public RestockReturnedOrderCommandHandler(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter, ICacheService cache, ILogger<RestockReturnedOrderCommandHandler> logger)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RestockReturnedOrderResult> Handle(RestockReturnedOrderCommand request, CancellationToken ct)
    {
        var result = new RestockReturnedOrderResult();
        var reference = ReferenceFor(request.OrderId);
        var consumed = await _uow.CostConsumptionLogs.GetBySourceOrderIdAsync(request.OrderId, ct);

        // one entry per product (an order can list a product twice)
        foreach (var line in request.Lines.Where(l => l.Quantity > 0).GroupBy(l => l.ProductId).Select(g => new ReturnedOrderLine { ProductId = g.Key, Quantity = g.Sum(l => l.Quantity) }))
        {
            if (await _uow.StockMovements.ExistsAsync(line.ProductId, reference, MovementType.Returned, ct))
            {
                result.AlreadyDone++;
                continue;
            }

            var inventory = await _uow.Inventories.GetByProductIdAsync(line.ProductId, ct);
            if (inventory == null)
            {
                _logger.LogWarning("Returned order {OrderId}: product {ProductId} is not in the warehouse — not restocked", request.OrderId, line.ProductId);
                result.Skipped++;
                continue;
            }

            var logs = consumed.Where(l => l.ProductId == line.ProductId && l.Quantity > 0).ToList();
            var unitCost = logs.Count > 0
                ? Math.Round(logs.Sum(l => l.TotalCost) / logs.Sum(l => l.Quantity), 4)
                : inventory.Cost;
            if (logs.Count == 0)
                _logger.LogWarning("Returned order {OrderId}: no cost was logged for product {ProductId}; restocked at its current cost {Cost}", request.OrderId, line.ProductId, unitCost);

            var previousQuantity = inventory.TotalQuantity;
            inventory.ReceiveStock(line.Quantity, unitCost);
            await _uow.Inventories.UpdateAsync(inventory, ct);

            var lot = new InventoryLot(line.ProductId, $"RET-{request.OrderId:N}"[..12], line.Quantity, unitCost, null, null);
            await _uow.InventoryLots.AddAsync(lot, ct);

            var movement = new StockMovement(
                Guid.Empty, // stamped with the current tenant on save
                line.ProductId,
                line.Quantity,
                MovementType.Returned,
                fromLocation: null,
                toLocation: null,
                reference: reference,
                reason: "Customer return",
                sourceOrderId: request.OrderId);
            await _uow.StockMovements.AddAsync(movement, ct);

            _outboxWriter.Stage(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                MovementType = nameof(MovementType.Returned),
                Reason = movement.Reason,
                SourceOrderId = request.OrderId,
                UnitCost = unitCost,
                ValueDelta = Math.Round(line.Quantity * unitCost, 2),
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            });

            _outboxWriter.Stage(new InventoryUpdatedEvent
            {
                ProductId = line.ProductId,
                PreviousQuantity = previousQuantity,
                CurrentQuantity = inventory.TotalQuantity,
                MovementType = nameof(MovementType.Returned),
                ReferenceId = request.OrderId,
                UpdatedAt = DateTime.UtcNow,
                EventCreatedAt = DateTime.UtcNow
            });

            result.Restocked++;
        }

        if (result.Restocked > 0)
        {
            await _uow.SaveChangesAsync(ct);
            foreach (var productId in request.Lines.Select(l => l.ProductId).Distinct())
                await _cache.RemoveAsync($"wh:inventory:{productId}", ct);
        }
        return result;
    }
}
