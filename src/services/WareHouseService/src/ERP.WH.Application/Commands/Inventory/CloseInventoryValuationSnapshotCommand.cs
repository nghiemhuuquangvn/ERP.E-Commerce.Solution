using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;

namespace ERP.WH.Application.Commands.Inventory;

public class CloseInventoryValuationSnapshotCommand : IRequest<InventoryValuationSnapshotDto>
{
    public required string PeriodCode { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class InventoryValuationSnapshotLineDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
}

public class InventoryValuationSnapshotDto
{
    public Guid Id { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public bool IsClosed { get; set; }
    public decimal TotalValue { get; set; }
    public List<InventoryValuationSnapshotLineDto> Lines { get; set; } = new();
}

/// <summary>
/// Closes a period-end valuation snapshot — immutable once written (the doc's "chốt kỳ"
/// rule: a closed period is never edited, only superseded by a correcting entry in a
/// later period). Reuses the same per-product FIFO/LIFO/Average calc as
/// GetInventoryValuationQueryHandler so the closed figure always matches what the live
/// query would have shown at close time.
/// </summary>
public class CloseInventoryValuationSnapshotCommandHandler : IRequestHandler<CloseInventoryValuationSnapshotCommand, InventoryValuationSnapshotDto>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IInventoryValuationSnapshotRepository _snapshots;
    private readonly IEventOutboxWriter _outboxWriter;

    public CloseInventoryValuationSnapshotCommandHandler(
        IWarehouseUnitOfWork uow,
        IInventoryValuationSnapshotRepository snapshots,
        IEventOutboxWriter outboxWriter)
    {
        _uow = uow;
        _snapshots = snapshots;
        _outboxWriter = outboxWriter;
    }

    public async Task<InventoryValuationSnapshotDto> Handle(CloseInventoryValuationSnapshotCommand request, CancellationToken cancellationToken)
    {
        var existing = await _snapshots.GetByPeriodAsync(request.PeriodCode, request.WarehouseId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Period {request.PeriodCode} is already closed and cannot be closed again.");

        var snapshot = new InventoryValuationSnapshot(request.PeriodCode, request.WarehouseId);

        var products = await _uow.Inventories.GetAllAsync(cancellationToken);
        foreach (var inventory in products)
        {
            if (inventory.ValuationMethod == ValuationMethod.Average)
            {
                snapshot.AddLine(inventory.ProductId, inventory.TotalQuantity, inventory.Cost);
                continue;
            }

            var lots = await _uow.InventoryLots.GetByProductIdAsync(inventory.ProductId, includeDepleted: false, cancellationToken);
            var totalQuantity = lots.Sum(l => l.RemainingQuantity);
            var totalValue = lots.Sum(l => l.RemainingQuantity * l.UnitCost);
            var averageUnitCost = totalQuantity > 0 ? totalValue / totalQuantity : inventory.Cost;

            snapshot.AddLine(inventory.ProductId, inventory.TotalQuantity, averageUnitCost);
        }

        await _snapshots.AddAsync(snapshot, cancellationToken);

        _outboxWriter.Stage(new InventoryValuationSnapshotClosed
        {
            SnapshotId = snapshot.Id,
            PeriodCode = snapshot.PeriodCode,
            WarehouseId = snapshot.WarehouseId,
            SnapshotDate = snapshot.SnapshotDate,
            TotalValue = snapshot.TotalValue,
            LineCount = snapshot.Lines.Count,
            EventCreatedAt = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync(cancellationToken);

        return Map(snapshot);
    }

    internal static InventoryValuationSnapshotDto Map(InventoryValuationSnapshot s) => new()
    {
        Id = s.Id,
        PeriodCode = s.PeriodCode,
        WarehouseId = s.WarehouseId,
        SnapshotDate = s.SnapshotDate,
        IsClosed = s.IsClosed,
        TotalValue = s.TotalValue,
        Lines = s.Lines.Select(l => new InventoryValuationSnapshotLineDto
        {
            ProductId = l.ProductId,
            Quantity = l.Quantity,
            UnitCost = l.UnitCost,
            TotalValue = l.TotalValue
        }).ToList()
    };
}

public interface IInventoryValuationSnapshotRepository
{
    Task<InventoryValuationSnapshot?> GetByPeriodAsync(string periodCode, Guid? warehouseId, CancellationToken cancellationToken);
    Task AddAsync(InventoryValuationSnapshot snapshot, CancellationToken cancellationToken);
}
