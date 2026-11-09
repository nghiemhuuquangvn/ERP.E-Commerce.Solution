using ERP.Shared.Infrastructure.Exceptions;
using ERP.WH.Application.Interfaces;
using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

public class StockAvailabilityDto
{
    public Guid ProductId { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    /// <summary>Current item cost (the value an issue is booked at under average costing).</summary>
    public decimal UnitCost { get; set; }
    /// <summary>Where it is: one row per warehouse and lot (only the warehouse asked for, when one is named).</summary>
    public List<StockBalanceDto> Balances { get; set; } = new();
}

/// <summary>What Manufacturing may plan with: the item totals and the balances per warehouse / lot (service-to-service, read only).</summary>
public class GetStockAvailabilityQuery : IRequest<StockAvailabilityDto>
{
    public Guid ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class GetStockAvailabilityQueryHandler(IWarehouseUnitOfWork uow, IMediator mediator) : IRequestHandler<GetStockAvailabilityQuery, StockAvailabilityDto>
{
    public async Task<StockAvailabilityDto> Handle(GetStockAvailabilityQuery request, CancellationToken ct)
    {
        var inventory = await uow.Inventories.GetByProductIdAsync(request.ProductId, ct)
            ?? throw new EntityNotFoundException("Inventory", request.ProductId);

        var balances = await mediator.Send(new GetStockBalancesQuery { ProductId = request.ProductId }, ct);
        if (request.WarehouseId.HasValue) balances = balances.Where(b => b.WarehouseId == request.WarehouseId).ToList();

        return new StockAvailabilityDto
        {
            ProductId = inventory.ProductId,
            TotalQuantity = inventory.TotalQuantity,
            ReservedQuantity = inventory.ReservedQuantity,
            LockedQuantity = inventory.LockedQuantity,
            AvailableQuantity = inventory.AvailableQuantity,
            UnitCost = inventory.Cost,
            Balances = balances
        };
    }
}
