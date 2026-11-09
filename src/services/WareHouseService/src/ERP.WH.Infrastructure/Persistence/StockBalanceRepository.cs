using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Queries.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class StockBalanceRepository(WarehouseDbContext context) : IStockBalanceRepository
{
    public async Task<StockBalance?> GetAsync(Guid productId, Guid? warehouseId, Guid? lotId, CancellationToken cancellationToken)
    {
        var added = context.StockBalances.Local.FirstOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId && b.LotId == lotId);
        if (added != null) return added;
        return await context.StockBalances.FirstOrDefaultAsync(b => b.ProductId == productId && b.WarehouseId == warehouseId && b.LotId == lotId, cancellationToken);
    }

    public async Task<List<StockBalance>> ListByProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var rows = await context.StockBalances.Where(b => b.ProductId == productId).ToListAsync(cancellationToken);
        // a row added earlier in this unit of work is tracked but not in the database yet
        foreach (var local in context.StockBalances.Local.Where(b => b.ProductId == productId && !rows.Contains(b))) rows.Add(local);
        return rows;
    }

    public async Task AddAsync(StockBalance balance, CancellationToken cancellationToken)
        => await context.StockBalances.AddAsync(balance, cancellationToken);

    public async Task<List<StockDriftDto>> FindDriftAsync(int take, CancellationToken cancellationToken)
    {
        // two small group-by reads, compared in memory (the tenant filter applies to every query)
        var byProduct = await context.StockBalances.GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(b => b.Quantity), Reserved = g.Sum(b => b.ReservedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var items = await context.Inventories.Select(i => new { i.ProductId, i.ProductName, i.TotalQuantity, i.ReservedQuantity }).ToListAsync(cancellationToken);

        var result = new List<StockDriftDto>();
        foreach (var item in items)
        {
            byProduct.TryGetValue(item.ProductId, out var sums);
            var quantity = sums?.Quantity ?? 0m;
            var reserved = sums?.Reserved ?? 0m;
            if (item.TotalQuantity != quantity)
                result.Add(new StockDriftDto { ProductId = item.ProductId, ProductName = item.ProductName, Kind = "Quantity", Expected = item.TotalQuantity, Actual = quantity });
            if (item.ReservedQuantity != reserved)
                result.Add(new StockDriftDto { ProductId = item.ProductId, ProductName = item.ProductName, Kind = "Reserved", Expected = item.ReservedQuantity, Actual = reserved });
        }

        var byLot = await context.StockBalances.Where(b => b.LotId != null).GroupBy(b => b.LotId!.Value)
            .Select(g => new { LotId = g.Key, Quantity = g.Sum(b => b.Quantity) })
            .ToDictionaryAsync(x => x.LotId, x => x.Quantity, cancellationToken);
        var lots = await context.InventoryLots.Select(l => new { l.LotId, l.ProductId, l.LotNumber, l.RemainingQuantity }).ToListAsync(cancellationToken);
        foreach (var lot in lots)
        {
            byLot.TryGetValue(lot.LotId, out var quantity);
            if (lot.RemainingQuantity != quantity)
                result.Add(new StockDriftDto { ProductId = lot.ProductId, LotId = lot.LotId, LotNumber = lot.LotNumber, Kind = "Lot", Expected = lot.RemainingQuantity, Actual = quantity });
        }

        return result.Take(take).ToList();
    }
}
