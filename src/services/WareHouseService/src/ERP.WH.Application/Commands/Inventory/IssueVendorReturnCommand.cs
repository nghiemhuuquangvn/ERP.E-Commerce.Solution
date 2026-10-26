using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>One product of a return to the supplier, as Purchasing names it (by code = the inventory SKU).</summary>
public class VendorReturnLine
{
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>Goods leave for the supplier (ROADMAP P1-8): the stock is issued and Accounting reverses the receipt accrual.</summary>
public class IssueVendorReturnCommand : IRequest<IssueVendorReturnResult>
{
    public Guid VendorReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public List<VendorReturnLine> Lines { get; set; } = new();
}

public class IssueVendorReturnResult
{
    public int Issued { get; set; }
    public int AlreadyDone { get; set; }
    public int Skipped { get; set; }
}

/// <summary>
/// Each line reduces the stock at the product's current cost and records a <see cref="MovementType.ReturnedToVendor"/> movement with a negative
/// value, which Accounting books as Dr Goods received not invoiced / Cr Inventory (the receipt accrual is reversed). All or nothing: if one line
/// asks for more than is available the command throws and nothing is changed (the consumer retries, then logs it). Idempotent per (return, product):
/// the movement reference is <c>vendor-return:&lt;returnId&gt;</c>. A product that is not in the warehouse is skipped with a warning.
/// </summary>
public class IssueVendorReturnCommandHandler : IRequestHandler<IssueVendorReturnCommand, IssueVendorReturnResult>
{
    public static string ReferenceFor(Guid vendorReturnId) => $"vendor-return:{vendorReturnId:N}";

    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;
    private readonly ILogger<IssueVendorReturnCommandHandler> _logger;

    public IssueVendorReturnCommandHandler(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter, ICacheService cache, ILogger<IssueVendorReturnCommandHandler> logger)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IssueVendorReturnResult> Handle(IssueVendorReturnCommand request, CancellationToken ct)
    {
        var result = new IssueVendorReturnResult();
        var reference = ReferenceFor(request.VendorReturnId);
        var touched = new List<Guid>();

        foreach (var line in request.Lines.Where(l => l.Quantity > 0 && !string.IsNullOrWhiteSpace(l.ProductCode))
                     .GroupBy(l => l.ProductCode, StringComparer.OrdinalIgnoreCase)
                     .Select(g => new VendorReturnLine { ProductCode = g.Key, Quantity = g.Sum(l => l.Quantity) }))
        {
            var inventory = await _uow.Inventories.GetByProductSkuAsync(line.ProductCode, ct);
            if (inventory == null)
            {
                _logger.LogWarning("Vendor return {Return}: product code {Code} is not in the warehouse; nothing issued", request.ReturnNumber, line.ProductCode);
                result.Skipped++;
                continue;
            }

            if (await _uow.StockMovements.ExistsAsync(inventory.ProductId, reference, MovementType.ReturnedToVendor, ct))
            {
                result.AlreadyDone++;
                continue;
            }

            if (inventory.AvailableQuantity < line.Quantity)
                throw new InvalidOperationException(
                    $"Vendor return {request.ReturnNumber}: {line.Quantity} of {line.ProductCode} cannot leave, only {inventory.AvailableQuantity} are available.");

            var previousQuantity = inventory.TotalQuantity;
            var unitCost = inventory.Cost;
            inventory.AdjustStock(-line.Quantity);
            await _uow.Inventories.UpdateAsync(inventory, ct);

            var movement = new StockMovement(
                Guid.Empty, // stamped with the current tenant on save
                inventory.ProductId,
                -line.Quantity,
                MovementType.ReturnedToVendor,
                fromLocation: null,
                toLocation: null,
                reference: reference,
                reason: $"Return {request.ReturnNumber} to supplier");
            await _uow.StockMovements.AddAsync(movement, ct);

            _outboxWriter.Stage(new StockMovementRecordedEvent
            {
                MovementId = movement.MovementId,
                ProductId = inventory.ProductId,
                Quantity = -line.Quantity,
                MovementType = nameof(MovementType.ReturnedToVendor),
                Reason = movement.Reason,
                UnitCost = unitCost,
                ValueDelta = -Math.Round(line.Quantity * unitCost, 2),
                RecordedAt = movement.RecordedAt,
                EventCreatedAt = DateTime.UtcNow
            });

            _outboxWriter.Stage(new InventoryUpdatedEvent
            {
                ProductId = inventory.ProductId,
                PreviousQuantity = previousQuantity,
                CurrentQuantity = inventory.TotalQuantity,
                MovementType = nameof(MovementType.ReturnedToVendor),
                ReferenceId = request.PurchaseOrderId,
                UpdatedAt = DateTime.UtcNow,
                EventCreatedAt = DateTime.UtcNow
            });

            touched.Add(inventory.ProductId);
            result.Issued++;
        }

        if (result.Issued > 0)
        {
            await _uow.SaveChangesAsync(ct);
            foreach (var productId in touched)
                await _cache.RemoveAsync($"wh:inventory:{productId}", ct);
        }
        return result;
    }
}
