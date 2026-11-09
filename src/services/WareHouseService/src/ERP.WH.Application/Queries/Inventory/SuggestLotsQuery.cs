using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

public class SuggestedLotDto
{
    public Guid LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal AvailableQuantity { get; set; }
    /// <summary>How much of the wanted quantity to take from this lot.</summary>
    public decimal Take { get; set; }
}

public class SuggestLotsResponse
{
    public Guid ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public List<SuggestedLotDto> Lots { get; set; } = new();
    /// <summary>What the lots cannot cover (it may still be covered by stock that is not in a lot).</summary>
    public decimal Shortfall { get; set; }
    /// <summary>Stock of the warehouse that is not in a lot and not held.</summary>
    public decimal LotLessAvailable { get; set; }
}

/// <summary>First-expiry-first suggestion (<see cref="LotPicker"/>): the same rule the reservation uses. Nothing is changed.</summary>
public class SuggestLotsQuery : IRequest<SuggestLotsResponse>
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Only the lots of this warehouse; empty = every warehouse.</summary>
    public Guid? WarehouseId { get; set; }
}

public class SuggestLotsQueryHandler(IWarehouseUnitOfWork uow) : IRequestHandler<SuggestLotsQuery, SuggestLotsResponse>
{
    public async Task<SuggestLotsResponse> Handle(SuggestLotsQuery request, CancellationToken ct)
    {
        var lots = await uow.InventoryLots.GetByProductIdAsync(request.ProductId, includeDepleted: false, ct);
        var restrict = request.WarehouseId.HasValue;
        var (picks, shortfall) = LotPicker.Pick(lots, request.Quantity, request.WarehouseId, restrict, DateTime.UtcNow);

        var pools = (await uow.StockBalances.ListByProductAsync(request.ProductId, ct))
            .Where(b => b.LotId == null && (!restrict || b.WarehouseId == request.WarehouseId))
            .Sum(b => Math.Max(b.Quantity - b.ReservedQuantity - b.LockedQuantity, 0));

        return new SuggestLotsResponse
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            Quantity = request.Quantity,
            Shortfall = shortfall,
            LotLessAvailable = pools,
            Lots = picks.Select(p => new SuggestedLotDto
            {
                LotId = p.Lot.LotId,
                LotNumber = p.Lot.LotNumber,
                WarehouseId = p.Lot.WarehouseId,
                ExpiryDate = p.Lot.ExpiryDate,
                AvailableQuantity = p.Lot.AvailableQuantity,
                Take = p.Take
            }).ToList()
        };
    }
}
