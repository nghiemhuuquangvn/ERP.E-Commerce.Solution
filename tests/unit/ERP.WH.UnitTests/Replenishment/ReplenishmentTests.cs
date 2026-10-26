using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Events;
using ERP.WH.Infrastructure.BackgroundJobs;
using FluentAssertions;
using Moq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Replenishment;

/// <summary>ROADMAP P1-9: min / max / safety stock, available-based low stock, one alert per time below the minimum, the daily scan.</summary>
public class ReplenishmentTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);

    private static InventoryEntity Stocked(int quantity, int min = 10, int max = 50, int safety = 3)
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), "Shirt", 0, 0, "SHIRT");
        inventory.ReceiveStock(quantity, 100m);
        inventory.SetStockPolicy(min, max, safety);
        return inventory;
    }

    [Fact]
    public void A_new_inventory_takes_its_policy_from_the_reorder_levels()
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), "Shirt", 100, 500);

        inventory.MinStock.Should().Be(100);
        inventory.MaxStock.Should().Be(600);
        inventory.SafetyStock.Should().Be(0);
    }

    [Theory]
    [InlineData(10, 50, -1)]   // negative safety
    [InlineData(10, 50, 11)]   // safety above the minimum
    [InlineData(10, 10, 3)]    // max not above min
    [InlineData(10, 5, 3)]     // max below min
    public void A_policy_that_breaks_safety_le_min_lt_max_is_refused(int min, int max, int safety)
    {
        var inventory = Stocked(100);

        var act = () => inventory.SetStockPolicy(min, max, safety);

        act.Should().Throw<ArgumentOutOfRangeException>();
        inventory.MinStock.Should().Be(10);
    }

    [Fact]
    public void The_policy_keeps_the_reorder_levels_in_step_for_older_readers()
    {
        var inventory = Stocked(100, min: 20, max: 80, safety: 5);

        inventory.ReorderPoint.Should().Be(20);
        inventory.ReorderQuantity.Should().Be(60);
    }

    [Fact]
    public void Low_stock_looks_at_available_stock_not_total()
    {
        var inventory = Stocked(15);   // min 10
        inventory.IsLowStock().Should().BeFalse();

        inventory.ReserveStock(8);     // 7 available, 15 in the building

        inventory.IsLowStock().Should().BeTrue();
    }

    [Fact]
    public void One_alert_is_raised_per_time_below_the_minimum_and_it_carries_the_policy()
    {
        var inventory = Stocked(9);

        var first = inventory.EvaluateLowStockAlert(Now);
        var second = inventory.EvaluateLowStockAlert(Now.AddMinutes(5));

        first.Should().NotBeNull();
        first!.ProductSku.Should().Be("SHIRT");
        first.AvailableQuantity.Should().Be(9);
        first.MinStock.Should().Be(10);
        first.MaxStock.Should().Be(50);
        first.SafetyStock.Should().Be(3);
        second.Should().BeNull();
    }

    [Fact]
    public void Going_back_above_the_minimum_clears_the_mark_so_the_next_drop_alerts_again()
    {
        var inventory = Stocked(9);
        inventory.EvaluateLowStockAlert(Now);

        inventory.ReceiveStock(40, 100m);
        inventory.EvaluateLowStockAlert(Now.AddHours(1)).Should().BeNull();
        inventory.LowStockAlertedAt.Should().BeNull();

        inventory.AdjustStock(-45);
        inventory.EvaluateLowStockAlert(Now.AddHours(2)).Should().NotBeNull();
    }

    [Fact]
    public void The_scan_repeats_the_alert_only_after_a_day()
    {
        var inventory = Stocked(9);
        inventory.EvaluateLowStockAlert(Now);

        inventory.EvaluateLowStockAlert(Now.AddHours(23), ReplenishmentScanService.RepeatAfter).Should().BeNull();
        inventory.EvaluateLowStockAlert(Now.AddHours(25), ReplenishmentScanService.RepeatAfter).Should().NotBeNull();
    }

    [Fact]
    public async Task The_scan_service_alerts_the_low_products_and_leaves_the_others()
    {
        var low = Stocked(5);
        var fine = Stocked(30);
        var uow = new Mock<IWarehouseUnitOfWork>();
        var inventories = new Mock<IInventoryRepository>();
        inventories.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<InventoryEntity> { low, fine });
        uow.SetupGet(u => u.Inventories).Returns(inventories.Object);
        var staged = new List<object>();
        var outbox = new Mock<IEventOutboxWriter>();
        outbox.Setup(o => o.Stage(It.IsAny<object>())).Callback<object>(staged.Add);

        var result = await new ReplenishmentScanService(uow.Object, outbox.Object).ScanAsync(default);

        result.Should().BeEquivalentTo(new ReplenishmentScanResult { Checked = 2, BelowMinimum = 1, AlertsSent = 1 });
        staged.Should().ContainSingle().Which.Should().BeOfType<StockAlertEvent>().Which.ProductId.Should().Be(low.ProductId);

        // A second run the same day sends nothing (the mark is set).
        staged.Clear();
        var again = await new ReplenishmentScanService(uow.Object, outbox.Object).ScanAsync(default);
        again.AlertsSent.Should().Be(0);
        staged.Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_the_policy_so_the_product_is_now_below_the_minimum_raises_the_alert()
    {
        var inventory = Stocked(20);   // min 10 → fine
        var uow = new Mock<IWarehouseUnitOfWork>();
        var inventories = new Mock<IInventoryRepository>();
        inventories.Setup(i => i.GetByProductIdAsync(inventory.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        uow.SetupGet(u => u.Inventories).Returns(inventories.Object);
        var staged = new List<object>();
        var outbox = new Mock<IEventOutboxWriter>();
        outbox.Setup(o => o.Stage(It.IsAny<object>())).Callback<object>(staged.Add);

        var response = await new UpdateStockPolicyCommandHandler(uow.Object, outbox.Object, Mock.Of<ICacheService>())
            .Handle(new UpdateStockPolicyCommand { ProductId = inventory.ProductId, MinStock = 25, MaxStock = 100, SafetyStock = 5 }, default);

        response.IsLowStock.Should().BeTrue();
        staged.Should().ContainSingle().Which.Should().BeOfType<StockAlertEvent>();
    }

    [Fact]
    public async Task A_policy_that_breaks_the_rules_is_a_refusal_not_a_crash()
    {
        var inventory = Stocked(20);
        var uow = new Mock<IWarehouseUnitOfWork>();
        var inventories = new Mock<IInventoryRepository>();
        inventories.Setup(i => i.GetByProductIdAsync(inventory.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        uow.SetupGet(u => u.Inventories).Returns(inventories.Object);

        var act = () => new UpdateStockPolicyCommandHandler(uow.Object, Mock.Of<IEventOutboxWriter>(), Mock.Of<ICacheService>())
            .Handle(new UpdateStockPolicyCommand { ProductId = inventory.ProductId, MinStock = 10, MaxStock = 10, SafetyStock = 0 }, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Maximum stock must be above the minimum*");
    }

    [Theory]
    [InlineData(1, 2, "2026-10-25", false)]   // before the hour, even though yesterday's run is old
    [InlineData(1, 2, "2026-10-26", false)]   // before the hour
    [InlineData(3, 2, "2026-10-26", false)]   // already ran today
    [InlineData(3, 2, "2026-10-25", true)]
    public void The_job_runs_once_per_day_from_the_configured_hour(int hour, int scanHour, string lastRun, bool due)
    {
        var now = new DateTime(2026, 10, 26, hour, 30, 0, DateTimeKind.Utc);

        ReplenishmentScanJob.IsDue(now, scanHour, DateTime.Parse(lastRun)).Should().Be(due);
    }
}
