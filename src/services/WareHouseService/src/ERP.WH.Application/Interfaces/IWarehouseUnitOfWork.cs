using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Commands.Stocktakes;
using ERP.WH.Application.Commands.Transfers;
using ERP.WH.Application.Services;

namespace ERP.WH.Application.Interfaces;

public interface IWarehouseUnitOfWork : IDisposable
{
    IInventoryRepository Inventories { get; }
    IReservationRepository Reservations { get; }
    IStockMovementRepository StockMovements { get; }
    IInventoryLotRepository InventoryLots { get; }
    IInventorySerialRepository InventorySerials { get; }
    ILocationRepository Locations { get; }
    ITransferOrderRepository TransferOrders { get; }
    IStocktakeRepository Stocktakes { get; }
    IInventoryLocationStockRepository InventoryLocationStocks { get; }
    IStockAdjustmentRepository StockAdjustments { get; }
    IWarehouseRepository Warehouses { get; }
    IBarcodeMappingRepository BarcodeMappings { get; }
    ICostConsumptionLogRepository CostConsumptionLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
    Task BeginTransactionAsync(CancellationToken ct);
    Task CommitTransactionAsync(CancellationToken ct);
    Task RollbackTransactionAsync(CancellationToken ct);
}
