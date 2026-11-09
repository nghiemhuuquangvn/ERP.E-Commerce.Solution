using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Queries.Inventory;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using Moq;

namespace ERP.WH.UnitTests.Support;

/// <summary>A real <see cref="StockLedger"/> over a mocked unit of work, with in-memory warehouse balances and no warehouses (every change lands "unassigned").</summary>
internal static class TestLedger
{
    public static IStockLedger For(Mock<IWarehouseUnitOfWork> uow, Guid? defaultWarehouse = null)
    {
        if (uow.Object.StockBalances == null) uow.SetupGet(u => u.StockBalances).Returns(new InMemoryBalances());
        if (uow.Object.InventoryLocationStocks == null)
        {
            var locations = new Mock<IInventoryLocationStockRepository>();
            locations.Setup(l => l.GetByProductIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<InventoryLocationStock>());
            uow.SetupGet(u => u.InventoryLocationStocks).Returns(locations.Object);
        }
        if (uow.Object.InventoryLots == null)
        {
            var lots = new Mock<IInventoryLotRepository>();
            lots.Setup(l => l.GetByProductIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<InventoryLot>());
            uow.SetupGet(u => u.InventoryLots).Returns(lots.Object);
        }

        var resolver = new Mock<IWarehouseResolver>();
        resolver.Setup(r => r.DefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(defaultWarehouse);
        resolver.Setup(r => r.ForLocationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        return new StockLedger(uow.Object, resolver.Object);
    }

    public sealed class InMemoryBalances : IStockBalanceRepository
    {
        public List<StockBalance> Rows { get; } = [];

        public Task<StockBalance?> GetAsync(Guid productId, Guid? warehouseId, Guid? lotId, CancellationToken cancellationToken)
            => Task.FromResult(Rows.FirstOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId && b.LotId == lotId));

        public Task<List<StockBalance>> ListByProductAsync(Guid productId, CancellationToken cancellationToken)
            => Task.FromResult(Rows.Where(b => b.ProductId == productId).ToList());

        public Task AddAsync(StockBalance balance, CancellationToken cancellationToken)
        {
            Rows.Add(balance);
            return Task.CompletedTask;
        }

        public Task<List<StockDriftDto>> FindDriftAsync(int take, CancellationToken cancellationToken) => Task.FromResult(new List<StockDriftDto>());
    }
}
