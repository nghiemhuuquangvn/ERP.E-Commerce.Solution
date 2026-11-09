using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class SourceReservationLineDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>The line of the source document (a production order component); returned unchanged.</summary>
    public Guid? SourceLineId { get; set; }
    /// <summary>Pins the line to one lot (it must be available in the warehouse); otherwise first-expiry-first.</summary>
    public Guid? LotId { get; set; }
}

/// <summary>
/// Holds stock for a source document in one warehouse (production order components). One open reservation per source: a repeat
/// answers the first one. Production reservations never expire; the source releases or consumes them (docs/business/Warehouse/ReservationsBySource.md).
/// </summary>
public class ReserveForSourceCommand : IRequest<SourceReservationDto>
{
    /// <summary>Only "ProductionOrder"; sales orders keep using <c>inventory/reserve</c>.</summary>
    public string SourceType { get; set; } = nameof(ReservationSource.ProductionOrder);
    public Guid SourceId { get; set; }
    /// <summary>E.g. the production order number, shown in lists.</summary>
    public string? SourceReference { get; set; }
    /// <summary>Warehouse the stock is held in; empty = the tenant's default warehouse.</summary>
    public Guid? WarehouseId { get; set; }
    public required List<SourceReservationLineDto> Lines { get; set; }
}

public class SourceReservationLotDto
{
    public Guid LotId { get; set; }
    public string? LotNumber { get; set; }
    public decimal Quantity { get; set; }
    public decimal Consumed { get; set; }
    public decimal Remaining { get; set; }
}

public class SourceReservationLineResultDto
{
    public Guid ProductId { get; set; }
    public Guid? SourceLineId { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Consumed { get; set; }
    public decimal Remaining { get; set; }
    public List<SourceReservationLotDto> Lots { get; set; } = new();
}

public class SourceReservationDto
{
    public Guid ReservationId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string? SourceReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ReservedAt { get; set; }
    public bool Replayed { get; set; }
    public List<SourceReservationLineResultDto> Lines { get; set; } = new();

    public static async Task<SourceReservationDto> FromAsync(Reservation r, IWarehouseUnitOfWork uow, bool replayed, CancellationToken ct)
    {
        var lotNumbers = new Dictionary<Guid, string>();
        foreach (var productId in r.ReservedItems.Select(i => i.ProductId).Distinct())
            foreach (var lot in await uow.InventoryLots.GetByProductIdAsync(productId, includeDepleted: true, ct))
                lotNumbers[lot.LotId] = lot.LotNumber;

        return new SourceReservationDto
        {
            ReservationId = r.ReservationId,
            SourceType = r.SourceType.ToString(),
            SourceId = r.OrderId,
            SourceReference = r.SourceReference,
            Status = r.Status.ToString(),
            ReservedAt = r.ReservedAt,
            Replayed = replayed,
            Lines = r.ReservedItems.Select(i => new SourceReservationLineResultDto
            {
                ProductId = i.ProductId,
                SourceLineId = i.SourceLineId,
                WarehouseId = i.WarehouseId,
                Quantity = i.Quantity,
                Consumed = i.ConsumedQuantity,
                Remaining = i.RemainingQuantity,
                Lots = i.LotAllocations.Select(a => new SourceReservationLotDto
                {
                    LotId = a.LotId,
                    LotNumber = lotNumbers.GetValueOrDefault(a.LotId),
                    Quantity = a.Quantity,
                    Consumed = a.Consumed,
                    Remaining = a.Remaining
                }).ToList()
            }).ToList()
        };
    }
}

public class ReserveForSourceCommandHandler(IWarehouseUnitOfWork uow, IStockLedger ledger, ICacheService cache)
    : IRequestHandler<ReserveForSourceCommand, SourceReservationDto>
{
    /// <summary>Production reservations are not time-limited.</summary>
    private static readonly DateTime NoExpiry = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    public async Task<SourceReservationDto> Handle(ReserveForSourceCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ReservationSource>(request.SourceType, ignoreCase: true, out var sourceType) || sourceType != ReservationSource.ProductionOrder)
            throw new BusinessRuleException("WH_RESERVATION_SOURCE", "Only production orders reserve through this call; sales orders use inventory/reserve.");
        if (request.SourceId == Guid.Empty)
            throw new BusinessRuleException("WH_RESERVATION_SOURCE", "The source document id is required.");
        if (request.Lines.Count == 0 || request.Lines.Any(l => l.Quantity <= 0))
            throw new BusinessRuleException("WH_QUANTITY", "Every line needs a quantity greater than zero.");

        // one open reservation per source: asking again answers the first one
        var open = (await uow.Reservations.GetByOrderIdAsync(request.SourceId, ct))
            .FirstOrDefault(r => r.SourceType == sourceType && r.Status == ReservationStatus.Reserved);
        if (open != null)
            return await SourceReservationDto.FromAsync(open, uow, replayed: true, ct);

        var warehouseId = (await ledger.TargetAsync(request.WarehouseId, null, ct)).WarehouseId;

        // two-phase: check every line first, so a shortfall holds nothing
        var inventories = new Dictionary<Guid, ERP.WH.Domain.Aggregates.Inventory.Inventory>();
        var wanted = new Dictionary<Guid, decimal>();
        var shortages = new List<string>();
        foreach (var line in request.Lines)
        {
            var inventory = await uow.Inventories.GetByProductIdAsync(line.ProductId, ct);
            wanted[line.ProductId] = wanted.GetValueOrDefault(line.ProductId) + line.Quantity;
            if (inventory == null) { shortages.Add($"{line.ProductId}: no stock record"); continue; }
            inventories[line.ProductId] = inventory;
        }
        foreach (var (productId, quantity) in wanted)
        {
            if (!inventories.TryGetValue(productId, out var inventory)) continue;
            var inWarehouse = await ledger.AvailableInWarehouseAsync(productId, warehouseId, ct);
            if (!inventory.CanReserve(quantity) || inWarehouse < quantity)
                shortages.Add($"{inventory.ProductName ?? productId.ToString()}: asked {quantity}, available in the warehouse {Math.Min(inWarehouse, inventory.AvailableQuantity)}");
        }
        if (shortages.Count > 0)
            throw new BusinessRuleException("WH_INSUFFICIENT_STOCK", "Not enough stock in the warehouse for: " + string.Join("; ", shortages));

        var items = new List<ReservedItem>();
        foreach (var line in request.Lines)
        {
            var inventory = inventories[line.ProductId];
            var held = await ledger.ReserveForSourceAsync(inventory, line.Quantity, warehouseId, line.LotId, ct);
            await uow.Inventories.UpdateAsync(inventory, ct);
            items.Add(new ReservedItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                WarehouseLocation = held.Location,
                LotAllocations = held.Allocations.ToList(),
                SourceLineId = line.SourceLineId,
                WarehouseId = warehouseId
            });
        }

        var reservation = new Reservation(request.SourceId, items, NoExpiry, sourceType, request.SourceReference?.Trim());
        await uow.Reservations.AddAsync(reservation, ct);
        await uow.SaveChangesAsync(ct);

        foreach (var productId in inventories.Keys)
            await cache.RemoveAsync($"wh:inventory:{productId}", ct);

        return await SourceReservationDto.FromAsync(reservation, uow, replayed: false, ct);
    }
}

public class GetSourceReservationQuery : IRequest<SourceReservationDto?>
{
    public string SourceType { get; set; } = nameof(ReservationSource.ProductionOrder);
    public Guid SourceId { get; set; }
}

public class GetSourceReservationQueryHandler(IWarehouseUnitOfWork uow) : IRequestHandler<GetSourceReservationQuery, SourceReservationDto?>
{
    public async Task<SourceReservationDto?> Handle(GetSourceReservationQuery request, CancellationToken ct)
    {
        if (!Enum.TryParse<ReservationSource>(request.SourceType, ignoreCase: true, out var sourceType))
            throw new BusinessRuleException("WH_RESERVATION_SOURCE", $"Unknown source type: {request.SourceType}");

        // the newest reservation of the source (an earlier one may have been released)
        var found = (await uow.Reservations.GetByOrderIdAsync(request.SourceId, ct))
            .Where(r => r.SourceType == sourceType)
            .OrderByDescending(r => r.ReservedAt)
            .FirstOrDefault();
        return found == null ? null : await SourceReservationDto.FromAsync(found, uow, replayed: false, ct);
    }
}
