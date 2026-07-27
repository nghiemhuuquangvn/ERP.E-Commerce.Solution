using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Commands.Stocktakes;
using ERP.WH.Application.Commands.Transfers;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.WH.Infrastructure.Persistence;

public class WarehouseUnitOfWork : IWarehouseUnitOfWork
{
    private readonly WarehouseDbContext _dbContext;
    private IDbContextTransaction? _currentTrans;

    public WarehouseUnitOfWork(
        WarehouseDbContext dbContext,
        IInventoryRepository inventories,
        IReservationRepository reservations,
        IStockMovementRepository stockMovements,
        IInventoryLotRepository inventoryLots,
        IInventorySerialRepository inventorySerials,
        ILocationRepository locations,
        ITransferOrderRepository transferOrders,
        IStocktakeRepository stocktakes,
        IInventoryLocationStockRepository inventoryLocationStocks,
        IStockAdjustmentRepository stockAdjustments,
        IWarehouseRepository warehouses,
        IBarcodeMappingRepository barcodeMappings,
        ICostConsumptionLogRepository costConsumptionLogs)
    {
        _dbContext = dbContext;
        Inventories = inventories;
        Reservations = reservations;
        StockMovements = stockMovements;
        InventoryLots = inventoryLots;
        InventorySerials = inventorySerials;
        Locations = locations;
        TransferOrders = transferOrders;
        Stocktakes = stocktakes;
        InventoryLocationStocks = inventoryLocationStocks;
        StockAdjustments = stockAdjustments;
        Warehouses = warehouses;
        BarcodeMappings = barcodeMappings;
        CostConsumptionLogs = costConsumptionLogs;
    }

    public IInventoryRepository Inventories { get; }
    public IReservationRepository Reservations { get; }
    public IStockMovementRepository StockMovements { get; }
    public IInventoryLotRepository InventoryLots { get; }
    public IInventorySerialRepository InventorySerials { get; }
    public ILocationRepository Locations { get; }
    public ITransferOrderRepository TransferOrders { get; }
    public IStocktakeRepository Stocktakes { get; }
    public IInventoryLocationStockRepository InventoryLocationStocks { get; }
    public IStockAdjustmentRepository StockAdjustments { get; }
    public IWarehouseRepository Warehouses { get; }
    public IBarcodeMappingRepository BarcodeMappings { get; }
    public ICostConsumptionLogRepository CostConsumptionLogs { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct) =>
        await _dbContext.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct)
    {
        _currentTrans = await _dbContext.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            if (_currentTrans != null) await _currentTrans.CommitAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            if (_currentTrans != null)
            {
                _currentTrans.Dispose();
                _currentTrans = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct)
    {
        if (_currentTrans != null)
        {
            await _currentTrans.RollbackAsync(ct);
            _currentTrans.Dispose();
            _currentTrans = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext.Dispose();
            _currentTrans?.Dispose();
        }
    }
}
