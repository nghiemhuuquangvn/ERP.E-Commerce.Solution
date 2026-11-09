using ERP.WH.Application.Queries.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Interfaces;

public interface IStockBalanceRepository
{
    /// <summary>The balance of one key, including a row added in this unit of work and not saved yet; null when none.</summary>
    Task<StockBalance?> GetAsync(Guid productId, Guid? warehouseId, Guid? lotId, CancellationToken cancellationToken);

    /// <summary>Every balance of a product (all warehouses and lots), including rows added in this unit of work and not saved yet.</summary>
    Task<List<StockBalance>> ListByProductAsync(Guid productId, CancellationToken cancellationToken);

    Task AddAsync(StockBalance balance, CancellationToken cancellationToken);

    /// <summary>Items and lots whose figures disagree with their balances (at most <paramref name="take"/> rows).</summary>
    Task<List<StockDriftDto>> FindDriftAsync(int take, CancellationToken cancellationToken);
}

/// <summary>Where stock without an explicit warehouse goes.</summary>
public interface IWarehouseResolver
{
    /// <summary>The warehouse of a location, or null when the location has none (or does not exist).</summary>
    Task<Guid?> ForLocationAsync(Guid locationId, CancellationToken cancellationToken);

    /// <summary>The tenant's default warehouse: the setting, else the active warehouse coded MAIN, else the first active warehouse by code; null when the tenant has no warehouse ("unassigned").</summary>
    Task<Guid?> DefaultAsync(CancellationToken cancellationToken);
}
