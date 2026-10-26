using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using ERP.WH.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Returns;

/// <summary>ROADMAP P1-5: the goods of a returned order go back on the shelf at the cost they left at, once.</summary>
public class RestockReturnedOrderTests
{
    private readonly Mock<IWarehouseUnitOfWork> _uow = new();
    private readonly Mock<IInventoryRepository> _inventories = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IInventoryLotRepository> _lots = new();
    private readonly Mock<ICostConsumptionLogRepository> _costLogs = new();
    private readonly Mock<IEventOutboxWriter> _outbox = new();
    private readonly List<StockMovement> _added = new();
    private readonly List<InventoryLot> _addedLots = new();
    private readonly List<object> _staged = new();
    private readonly List<CostConsumptionLog> _logs = new();
    private readonly Guid _orderId = Guid.NewGuid();

    public RestockReturnedOrderTests()
    {
        _uow.SetupGet(u => u.Inventories).Returns(_inventories.Object);
        _uow.SetupGet(u => u.StockMovements).Returns(_movements.Object);
        _uow.SetupGet(u => u.InventoryLots).Returns(_lots.Object);
        _uow.SetupGet(u => u.CostConsumptionLogs).Returns(_costLogs.Object);
        _movements.Setup(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => _added.Add(m)).Returns(Task.CompletedTask);
        _movements.Setup(m => m.ExistsAsync(It.IsAny<Guid>(), It.IsAny<string?>(), MovementType.Returned, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid p, string? r, MovementType _, CancellationToken __) => _added.Any(a => a.ProductId == p && a.Reference == r));
        _lots.Setup(l => l.AddAsync(It.IsAny<InventoryLot>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryLot, CancellationToken>((l, _) => _addedLots.Add(l)).Returns(Task.CompletedTask);
        _costLogs.Setup(c => c.GetBySourceOrderIdAsync(_orderId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _logs.ToList());
        _outbox.Setup(o => o.Stage(It.IsAny<object>())).Callback<object>(e => _staged.Add(e));
    }

    private RestockReturnedOrderCommandHandler Handler() =>
        new(_uow.Object, _outbox.Object, Mock.Of<ICacheService>(), NullLogger<RestockReturnedOrderCommandHandler>.Instance);

    private InventoryEntity Stocked(Guid productId, int quantity, decimal cost)
    {
        var inventory = new InventoryEntity(productId, "Ao thun", 0);
        inventory.ReceiveStock(quantity, cost);
        _inventories.Setup(i => i.GetByProductIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        return inventory;
    }

    private void IssuedAt(Guid productId, int quantity, decimal unitCost) => _logs.Add(new CostConsumptionLog(productId, null, quantity, unitCost, _orderId));

    private RestockReturnedOrderCommand Command(params (Guid Product, int Qty)[] lines) => new()
    {
        OrderId = _orderId,
        Lines = lines.Select(l => new ReturnedOrderLine { ProductId = l.Product, Quantity = l.Qty }).ToList(),
    };

    [Fact]
    public async Task The_goods_come_back_at_the_cost_they_left_at_with_a_lot_a_movement_and_a_positive_value()
    {
        var productId = Guid.NewGuid();
        var inventory = Stocked(productId, 10, 70m);              // the product costs 70 now ...
        IssuedAt(productId, 3, 50m);                              // ... but the order issued 3 at 50

        var result = await Handler().Handle(Command((productId, 3)), default);

        result.Restocked.Should().Be(1);
        inventory.TotalQuantity.Should().Be(13);
        var movement = _added.Single();
        (movement.MovementType, movement.Quantity, movement.Reference, movement.SourceOrderId).Should().Be((MovementType.Returned, 3, $"return:{_orderId:N}", (Guid?)_orderId));
        var lot = _addedLots.Single();
        (lot.UnitCost, lot.RemainingQuantity).Should().Be((50m, 3));
        var evt = _staged.OfType<StockMovementRecordedEvent>().Single();
        (evt.MovementType, evt.ValueDelta, evt.UnitCost, evt.SourceOrderId).Should().Be(("Returned", 150m, 50m, (Guid?)_orderId));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Several_cost_rows_are_averaged_by_quantity()
    {
        var productId = Guid.NewGuid();
        Stocked(productId, 10, 70m);
        IssuedAt(productId, 2, 40m);
        IssuedAt(productId, 1, 70m);                              // 2 at 40 + 1 at 70 -> 150 / 3 = 50

        await Handler().Handle(Command((productId, 3)), default);

        _staged.OfType<StockMovementRecordedEvent>().Single().ValueDelta.Should().Be(150m);
    }

    [Fact]
    public async Task An_order_with_no_cost_log_comes_back_at_the_current_cost()
    {
        var productId = Guid.NewGuid();
        Stocked(productId, 10, 70m);

        await Handler().Handle(Command((productId, 2)), default);

        _staged.OfType<StockMovementRecordedEvent>().Single().ValueDelta.Should().Be(140m);
    }

    [Fact]
    public async Task The_same_event_twice_restocks_once()
    {
        var productId = Guid.NewGuid();
        var inventory = Stocked(productId, 10, 70m);

        await Handler().Handle(Command((productId, 3)), default);
        var second = await Handler().Handle(Command((productId, 3)), default);

        second.AlreadyDone.Should().Be(1);
        second.Restocked.Should().Be(0);
        inventory.TotalQuantity.Should().Be(13);
        _added.Should().ContainSingle();
    }

    [Fact]
    public async Task A_product_the_warehouse_does_not_track_is_skipped_and_the_others_still_come_back()
    {
        var known = Guid.NewGuid();
        Stocked(known, 5, 10m);
        var unknown = Guid.NewGuid();
        _inventories.Setup(i => i.GetByProductIdAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((InventoryEntity?)null);

        var result = await Handler().Handle(Command((unknown, 1), (known, 2)), default);

        (result.Restocked, result.Skipped).Should().Be((1, 1));
        _added.Single().ProductId.Should().Be(known);
    }

    [Fact]
    public async Task The_same_product_on_two_lines_is_restocked_as_one_movement()
    {
        var productId = Guid.NewGuid();
        var inventory = Stocked(productId, 0, 10m);

        await Handler().Handle(Command((productId, 2), (productId, 3)), default);

        inventory.TotalQuantity.Should().Be(5);
        _added.Should().ContainSingle().Which.Quantity.Should().Be(5);
    }

    [Fact]
    public void The_event_of_Sales_is_read_by_Warehouse_with_its_lines()
    {
        var productId = Guid.NewGuid();
        var json = $"{{\"OrderId\":\"{_orderId}\",\"TenantId\":\"{Guid.NewGuid()}\",\"RefundAmount\":100,\"Lines\":[{{\"ProductId\":\"{productId}\",\"Quantity\":3}}]}}";

        var contract = JsonSerializer.Deserialize<SalesOrderReturnedContract>(json)!;

        contract.OrderId.Should().Be(_orderId);
        contract.Lines.Should().ContainSingle().Which.Should().Match<SalesOrderReturnedLine>(l => l.ProductId == productId && l.Quantity == 3);
    }
}
