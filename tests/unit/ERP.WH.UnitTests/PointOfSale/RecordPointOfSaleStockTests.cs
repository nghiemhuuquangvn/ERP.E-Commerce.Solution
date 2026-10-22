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
using Newtonsoft.Json.Linq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.PointOfSale;

public class RecordPointOfSaleStockTests
{
    private readonly Mock<IWarehouseUnitOfWork> _uow = new();
    private readonly Mock<IInventoryRepository> _inventories = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<ICostConsumptionLogRepository> _costLogs = new();
    private readonly Mock<IEventOutboxWriter> _outbox = new();
    private readonly List<StockMovement> _added = new();

    public RecordPointOfSaleStockTests()
    {
        _uow.SetupGet(u => u.Inventories).Returns(_inventories.Object);
        _uow.SetupGet(u => u.StockMovements).Returns(_movements.Object);
        _uow.SetupGet(u => u.CostConsumptionLogs).Returns(_costLogs.Object);
        _movements.Setup(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => _added.Add(m))
            .Returns(Task.CompletedTask);
    }

    private RecordPointOfSaleStockCommandHandler Handler() =>
        new(_uow.Object, _outbox.Object, Mock.Of<ICacheService>(), NullLogger<RecordPointOfSaleStockCommandHandler>.Instance);

    private InventoryEntity Stocked(Guid productId, int quantity, int reorderPoint = 0)
    {
        var inventory = new InventoryEntity(productId, "Ao thun", reorderPoint);
        if (quantity > 0) inventory.ReceiveStock(quantity, 50m);
        _inventories.Setup(i => i.GetByProductIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        return inventory;
    }

    private static RecordPointOfSaleStockCommand Sale(Guid transactionId, Guid productId, int quantity, bool isReturn = false) => new()
    {
        TransactionId = transactionId,
        IsReturn = isReturn,
        Lines = { new PointOfSaleLine { ProductId = productId, ProductName = "Ao thun", Quantity = quantity } }
    };

    [Fact]
    public async Task A_sale_takes_the_quantity_out_of_stock()
    {
        var productId = Guid.NewGuid();
        var inventory = Stocked(productId, 10);

        var result = await Handler().Handle(Sale(Guid.NewGuid(), productId, 3), default);

        result.LinesApplied.Should().Be(1);
        inventory.TotalQuantity.Should().Be(7);
        _added.Should().ContainSingle(m => m.MovementType == MovementType.Sold && m.Quantity == 3);
        _costLogs.Verify(c => c.AddAsync(It.IsAny<CostConsumptionLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_sale_beyond_stock_goes_negative_and_raises_a_stock_alert()
    {
        var productId = Guid.NewGuid();
        var inventory = Stocked(productId, 1);

        await Handler().Handle(Sale(Guid.NewGuid(), productId, 3), default);

        inventory.TotalQuantity.Should().Be(-2);
        _outbox.Verify(o => o.Stage(It.IsAny<StockAlertEvent>()), Times.Once);
    }

    [Fact]
    public async Task A_redelivered_sale_is_not_applied_twice()
    {
        var productId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var inventory = Stocked(productId, 10);
        _movements.Setup(m => m.ExistsAsync(productId, $"POS-{transactionId}", MovementType.Sold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(Sale(transactionId, productId, 3), default);

        result.LinesAlreadyApplied.Should().Be(1);
        inventory.TotalQuantity.Should().Be(10);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_voided_sale_puts_the_quantity_back()
    {
        var productId = Guid.NewGuid();
        var inventory = Stocked(productId, 7);

        await Handler().Handle(Sale(Guid.NewGuid(), productId, 3, isReturn: true), default);

        inventory.TotalQuantity.Should().Be(10);
        _added.Should().ContainSingle(m => m.Reference!.StartsWith("POS-VOID-"));
    }

    [Fact]
    public async Task A_product_without_inventory_is_skipped()
    {
        var result = await Handler().Handle(Sale(Guid.NewGuid(), Guid.NewGuid(), 1), default);

        result.LinesWithoutInventory.Should().Be(1);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("ERP.POS.Domain.Events.TransactionCreatedEvent, ERP.POS.Domain", false)]
    [InlineData("ERP.POS.Domain.Events.TransactionVoidedEvent, ERP.POS.Domain", true)]
    public void Pos_topic_events_map_to_the_stock_command(string type, bool isReturn)
    {
        var productId = Guid.NewGuid();
        var raw = JObject.FromObject(new
        {
            TransactionId = Guid.NewGuid(),
            Lines = new[] { new { ProductId = productId, ProductName = "Ao thun", Quantity = 2 } }
        });
        raw["$type"] = type;

        var command = PosTransactionConsumer.ToCommand(raw);

        command!.IsReturn.Should().Be(isReturn);
        command.Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 2);
    }

    [Fact]
    public void Other_pos_topic_events_are_ignored()
    {
        var raw = JObject.FromObject(new { TransactionId = Guid.NewGuid() });
        raw["$type"] = "ERP.POS.Domain.Events.CashShiftClosedEvent, ERP.POS.Domain";

        PosTransactionConsumer.ToCommand(raw).Should().BeNull();
    }
}
