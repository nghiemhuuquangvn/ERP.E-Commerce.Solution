using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.StockCost;

/// <summary>ROADMAP P1-1b: every StockMovementRecordedEvent says what the movement did to the stock value.</summary>
public class StockMovementCostTests
{
    private readonly Mock<IWarehouseUnitOfWork> _uow = new();
    private readonly Mock<IInventoryRepository> _inventories = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<ICostConsumptionLogRepository> _costLogs = new();
    private readonly Mock<IInventoryLotRepository> _lots = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IEventOutboxWriter> _outbox = new();
    private readonly List<StockMovementRecordedEvent> _events = new();

    public StockMovementCostTests()
    {
        _uow.SetupGet(u => u.Inventories).Returns(_inventories.Object);
        _uow.SetupGet(u => u.StockMovements).Returns(_movements.Object);
        _uow.SetupGet(u => u.CostConsumptionLogs).Returns(_costLogs.Object);
        _uow.SetupGet(u => u.InventoryLots).Returns(_lots.Object);
        _uow.SetupGet(u => u.Reservations).Returns(_reservations.Object);
        _outbox.Setup(o => o.Stage(It.IsAny<StockMovementRecordedEvent>()))
            .Callback<object>(e => _events.Add((StockMovementRecordedEvent)e));
    }

    private InventoryEntity Stocked(Guid productId, int quantity, decimal cost)
    {
        var inventory = new InventoryEntity(productId, "Ao thun", 0);
        inventory.ReceiveStock(quantity, cost);
        _inventories.Setup(i => i.GetByProductIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        return inventory;
    }

    [Fact]
    public async Task A_priced_receipt_adds_quantity_times_cost_to_the_stock_value()
    {
        var productId = Guid.NewGuid();
        _inventories.Setup(i => i.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryEntity?)null);
        var handler = new ReceiveStockCommandHandler(_uow.Object, _outbox.Object, Mock.Of<ICacheService>());

        await handler.Handle(new ReceiveStockCommand
        {
            ProductId = productId, Quantity = 10, UnitCost = 50m, Reference = "PO-1-grn", PurchaseOrderId = Guid.NewGuid()
        }, default);

        var evt = _events.Single();
        evt.MovementType.Should().Be("Received");
        evt.UnitCost.Should().Be(50m);
        evt.ValueDelta.Should().Be(500m);
    }

    [Fact]
    public async Task A_receipt_without_a_price_adds_no_value()
    {
        var productId = Guid.NewGuid();
        _inventories.Setup(i => i.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryEntity?)null);
        var handler = new ReceiveStockCommandHandler(_uow.Object, _outbox.Object, Mock.Of<ICacheService>());

        await handler.Handle(new ReceiveStockCommand { ProductId = productId, Quantity = 10, UnitCost = 0m }, default);

        _events.Single().ValueDelta.Should().Be(0m);
    }

    [Fact]
    public async Task A_shipped_plain_product_leaves_at_the_inventory_cost()
    {
        var productId = Guid.NewGuid();
        Stocked(productId, 10, 40m);
        var reservation = new Reservation(Guid.NewGuid(),
            new List<ReservedItem> { new() { ProductId = productId, Quantity = 3, WarehouseLocation = "A" } },
            DateTime.UtcNow.AddHours(1));
        var service = new ReservationFulfillmentService(_uow.Object, _outbox.Object, Mock.Of<ICacheService>());

        await service.ConsumeAsync(reservation, default);

        var evt = _events.Single();
        evt.MovementType.Should().Be("Sold");
        evt.UnitCost.Should().Be(40m);
        evt.ValueDelta.Should().Be(-120m);
    }

    [Fact]
    public async Task A_shipped_lot_product_leaves_at_the_cost_of_each_lot()
    {
        var productId = Guid.NewGuid();
        Stocked(productId, 10, 40m);
        var cheap = new InventoryLot(productId, "L1", 5, 10m, null, null);
        var dear = new InventoryLot(productId, "L2", 5, 30m, null, null);
        _lots.Setup(l => l.GetByIdAsync(cheap.LotId, It.IsAny<CancellationToken>())).ReturnsAsync(cheap);
        _lots.Setup(l => l.GetByIdAsync(dear.LotId, It.IsAny<CancellationToken>())).ReturnsAsync(dear);
        var reservation = new Reservation(Guid.NewGuid(),
            new List<ReservedItem>
            {
                new()
                {
                    ProductId = productId, Quantity = 4, WarehouseLocation = "A",
                    LotAllocations = { new LotAllocation { LotId = cheap.LotId, Quantity = 3 }, new LotAllocation { LotId = dear.LotId, Quantity = 1 } }
                }
            },
            DateTime.UtcNow.AddHours(1));
        var service = new ReservationFulfillmentService(_uow.Object, _outbox.Object, Mock.Of<ICacheService>());

        await service.ConsumeAsync(reservation, default);

        _events.Single().ValueDelta.Should().Be(-(3 * 10m + 1 * 30m));
    }

    [Fact]
    public async Task A_pos_sale_and_its_void_move_the_value_in_opposite_directions()
    {
        var productId = Guid.NewGuid();
        Stocked(productId, 10, 50m);
        var handler = new RecordPointOfSaleStockCommandHandler(
            _uow.Object, _outbox.Object, Mock.Of<ICacheService>(), NullLogger<RecordPointOfSaleStockCommandHandler>.Instance);

        RecordPointOfSaleStockCommand Cmd(bool isReturn) => new()
        {
            TransactionId = Guid.NewGuid(), IsReturn = isReturn,
            Lines = { new PointOfSaleLine { ProductId = productId, ProductName = "Ao thun", Quantity = 2 } }
        };

        await handler.Handle(Cmd(false), default);
        await handler.Handle(Cmd(true), default);

        _events.Select(e => (e.MovementType, e.ValueDelta)).Should()
            .Equal(("Sold", -100m), ("Adjusted", 100m));
    }
}
