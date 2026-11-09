using ERP.WH.Application.Interfaces;
using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

/// <summary>One row of "how much is where": a warehouse (null = unassigned) and a lot (null = not attributed to a lot).</summary>
public class StockBalanceDto
{
    public Guid? WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? LotStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
}

public class GetStockBalancesQuery : IRequest<List<StockBalanceDto>>
{
    public Guid ProductId { get; set; }
}

public class GetStockBalancesQueryHandler(IWarehouseUnitOfWork uow) : IRequestHandler<GetStockBalancesQuery, List<StockBalanceDto>>
{
    public async Task<List<StockBalanceDto>> Handle(GetStockBalancesQuery request, CancellationToken cancellationToken)
    {
        var balances = await uow.StockBalances.ListByProductAsync(request.ProductId, cancellationToken);
        var warehouses = (await uow.Warehouses.GetAllAsync(cancellationToken)).ToDictionary(w => w.Id);
        var lots = (await uow.InventoryLots.GetByProductIdAsync(request.ProductId, includeDepleted: true, cancellationToken)).ToDictionary(l => l.LotId);

        return balances
            .Where(b => b.Quantity != 0 || b.ReservedQuantity != 0 || b.LockedQuantity != 0)
            .Select(b =>
            {
                warehouses.TryGetValue(b.WarehouseId ?? Guid.Empty, out var warehouse);
                lots.TryGetValue(b.LotId ?? Guid.Empty, out var lot);
                return new StockBalanceDto
                {
                    WarehouseId = b.WarehouseId,
                    WarehouseCode = warehouse?.Code,
                    WarehouseName = warehouse?.Name,
                    LotId = b.LotId,
                    LotNumber = lot?.LotNumber,
                    LotStatus = lot?.Status.ToString(),
                    ExpiryDate = lot?.ExpiryDate,
                    Quantity = b.Quantity,
                    ReservedQuantity = b.ReservedQuantity,
                    LockedQuantity = b.LockedQuantity,
                    AvailableQuantity = b.AvailableQuantity
                };
            })
            .OrderBy(r => r.WarehouseCode ?? "~").ThenBy(r => r.LotNumber ?? "")
            .ToList();
    }
}

/// <summary>
/// Items whose figures disagree: the item total (<c>Inventories</c>) against the sum of its balances, and each lot against its balance.
/// Quantity differences are a defect; a reserved difference can also come from the older reserve / sale code (a manual sale lowers the item's
/// reservation without releasing a lot), which is why it is listed apart.
/// </summary>
public class StockDriftDto
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string Kind { get; set; } = string.Empty;
    public decimal Expected { get; set; }
    public decimal Actual { get; set; }
}

public class GetStockReconciliationQuery : IRequest<List<StockDriftDto>>
{
    public int Take { get; set; } = 200;
}

public class GetStockReconciliationQueryHandler(IStockBalanceRepository balances) : IRequestHandler<GetStockReconciliationQuery, List<StockDriftDto>>
{
    public Task<List<StockDriftDto>> Handle(GetStockReconciliationQuery request, CancellationToken cancellationToken)
        => balances.FindDriftAsync(Math.Clamp(request.Take, 1, 1000), cancellationToken);
}
