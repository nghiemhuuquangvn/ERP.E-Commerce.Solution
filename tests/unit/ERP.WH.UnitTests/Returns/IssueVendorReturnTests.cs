using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Returns;

/// <summary>ROADMAP P1-8: goods sent back to the supplier leave the stock once, at the current cost, with a negative-value movement.</summary>
public class IssueVendorReturnTests
{
    private readonly Mock<IWarehouseUnitOfWork> _uow = new();
    private readonly Mock<IInventoryRepository> _inventories = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IEventOutboxWriter> _outbox = new();
    private readonly List<StockMovement> _added = new();
    private readonly List<object> _staged = new();
    private readonly Guid _returnId = Guid.NewGuid();

    public IssueVendorReturnTests()
    {
        _uow.SetupGet(u => u.Inventories).Returns(_inventories.Object);
        _uow.SetupGet(u => u.StockMovements).Returns(_movements.Object);
        _movements.Setup(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => _added.Add(m)).Returns(Task.CompletedTask);
        _movements.Setup(m => m.ExistsAsync(It.IsAny<Guid>(), It.IsAny<string?>(), MovementType.ReturnedToVendor, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid p, string? r, MovementType _, CancellationToken __) => _added.Any(a => a.ProductId == p && a.Reference == r));
        _outbox.Setup(o => o.Stage(It.IsAny<object>())).Callback<object>(e => _staged.Add(e));
    }

    private IssueVendorReturnCommandHandler Handler() =>
        new(_uow.Object, _outbox.Object, Mock.Of<ICacheService>(), NullLogger<IssueVendorReturnCommandHandler>.Instance);

    private InventoryEntity Stocked(string sku, int quantity, decimal cost)
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), "Shirt", 0, productSku: sku);
        inventory.ReceiveStock(quantity, cost);
        _inventories.Setup(i => i.GetByProductSkuAsync(sku, It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        return inventory;
    }

    private IssueVendorReturnCommand Command(params (string Code, int Qty)[] lines) => new()
    {
        VendorReturnId = _returnId, ReturnNumber = "VR-1", PurchaseOrderId = Guid.NewGuid(),
        Lines = lines.Select(l => new VendorReturnLine { ProductCode = l.Code, Quantity = l.Qty }).ToList()
    };

    [Fact]
    public async Task The_stock_leaves_at_the_current_cost_with_a_negative_value_movement_and_events()
    {
        var inventory = Stocked("SHIRT", 10, 100m);

        var result = await Handler().Handle(Command(("SHIRT", 3)), default);

        result.Issued.Should().Be(1);
        inventory.TotalQuantity.Should().Be(7);
        var movement = _added.Single();
        (movement.MovementType, movement.Quantity, movement.Reference).Should().Be((MovementType.ReturnedToVendor, -3, $"vendor-return:{_returnId:N}"));
        var recorded = _staged.OfType<StockMovementRecordedEvent>().Single();
        recorded.MovementType.Should().Be("ReturnedToVendor");
        recorded.ValueDelta.Should().Be(-300m);
        _staged.OfType<InventoryUpdatedEvent>().Single().CurrentQuantity.Should().Be(7);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_redelivered_event_issues_nothing_twice()
    {
        var inventory = Stocked("SHIRT", 10, 100m);
        var handler = Handler();

        await handler.Handle(Command(("SHIRT", 3)), default);
        var second = await handler.Handle(Command(("SHIRT", 3)), default);

        second.AlreadyDone.Should().Be(1);
        second.Issued.Should().Be(0);
        inventory.TotalQuantity.Should().Be(7);
        _added.Should().HaveCount(1);
    }

    [Fact]
    public async Task More_than_is_available_refuses_the_whole_return_and_changes_nothing()
    {
        var shirt = Stocked("SHIRT", 10, 100m);
        var pants = Stocked("PANTS", 1, 50m);

        var act = () => Handler().Handle(Command(("SHIRT", 2), ("PANTS", 2)), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PANTS*only 1*");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _staged.OfType<StockMovementRecordedEvent>().Count().Should().Be(1, "the first line was staged but never saved: the next attempt starts again");
    }

    [Fact]
    public async Task Reserved_stock_cannot_be_sent_back_and_an_unknown_product_is_skipped()
    {
        var inventory = Stocked("SHIRT", 10, 100m);
        inventory.ReserveStock(9);
        _inventories.Setup(i => i.GetByProductSkuAsync("HAT", It.IsAny<CancellationToken>())).ReturnsAsync((InventoryEntity?)null);

        await ((Func<Task>)(() => Handler().Handle(Command(("SHIRT", 2)), default))).Should().ThrowAsync<InvalidOperationException>();

        var result = await Handler().Handle(Command(("HAT", 1)), default);
        result.Skipped.Should().Be(1);
        result.Issued.Should().Be(0);
    }
}
