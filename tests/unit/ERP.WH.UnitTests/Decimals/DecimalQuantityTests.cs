using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Aggregates.Stocktakes;
using ERP.WH.Domain.Aggregates.Transfers;
using ERP.WH.Domain.Events;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Decimals;

/// <summary>Warehouse upgrade W1: every stock quantity is decimal. Whole numbers behave as before; fractions (kg, litre, m3) are exact.</summary>
public class DecimalQuantityTests
{
    private static InventoryEntity Stock(decimal total = 0, ValuationMethod method = ValuationMethod.Average)
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), "Paint", reorderPoint: 2.5m, reorderQuantity: 10m);
        inventory.SetValuationMethod(method);
        if (total > 0) inventory.ReceiveStock(total, 10m);
        return inventory;
    }

    [Fact]
    public void Receive_reserve_and_sell_keep_fractions_exactly()
    {
        var inventory = Stock(2.5m);
        inventory.ReserveStock(1.25m);
        inventory.AvailableQuantity.Should().Be(1.25m);

        inventory.SellStock(1.25m);
        inventory.TotalQuantity.Should().Be(1.25m);
        inventory.ReservedQuantity.Should().Be(0m);
    }

    [Fact]
    public void Reserving_more_than_the_fraction_available_is_refused()
    {
        var inventory = Stock(2.5m);
        inventory.ReserveStock(2.25m);
        inventory.CanReserve(0.5m).Should().BeFalse();
        inventory.CanReserve(0.25m).Should().BeTrue();
    }

    [Fact]
    public void Average_cost_weights_fractional_receipts()
    {
        var inventory = Stock(2.5m);                  // 2.5 at 10
        inventory.ReceiveStock(1.5m, 20m);            // 1.5 at 20 -> (25 + 30) / 4
        inventory.Cost.Should().Be(13.75m);
        inventory.TotalQuantity.Should().Be(4m);
    }

    [Fact]
    public void Adjusting_by_a_fraction_and_the_low_stock_rule_use_decimals()
    {
        var inventory = Stock(5m);
        inventory.AdjustStock(-2.25m);
        inventory.TotalQuantity.Should().Be(2.75m);
        inventory.SetStockPolicy(2.5m, 12.5m, 1.25m);
        inventory.MinStock.Should().Be(2.5m);
        inventory.ReorderQuantity.Should().Be(10m);
        inventory.IsLowStock().Should().BeFalse();
        inventory.AdjustStock(-0.25m);
        inventory.IsLowStock().Should().BeTrue();   // available 2.5 <= minimum 2.5
    }

    [Fact]
    public void Lot_reserve_consume_and_depletion_are_exact()
    {
        var lot = new InventoryLot(Guid.NewGuid(), "L1", 2.5m, 8m);
        lot.Reserve(1.25m);
        lot.AvailableQuantity.Should().Be(1.25m);
        lot.Consume(1.25m);
        lot.RemainingQuantity.Should().Be(1.25m);
        lot.ReservedQuantity.Should().Be(0m);
        lot.IsDepleted().Should().BeFalse();
        lot.Deplete(1.25m);
        lot.IsDepleted().Should().BeTrue();
    }

    [Fact]
    public void A_lot_cannot_give_more_than_it_holds_even_by_a_fraction()
    {
        var lot = new InventoryLot(Guid.NewGuid(), "L1", 1m, 8m);
        var act = () => lot.Deplete(1.000001m);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Location_stock_reserves_and_transfers_fractions()
    {
        var from = new InventoryLocationStock(Guid.NewGuid(), Guid.NewGuid());
        var to = new InventoryLocationStock(from.ProductId, Guid.NewGuid());
        from.Receive(3.5m);
        from.ReserveStock(1m);
        from.TransferOut(2.5m);
        to.TransferIn(2.5m);
        from.Quantity.Should().Be(1m);
        to.Quantity.Should().Be(2.5m);
        from.AvailableQuantity.Should().Be(0m);
    }

    [Fact]
    public void Stocktake_variance_can_be_a_fraction()
    {
        var line = new StocktakeLine(Guid.NewGuid(), Guid.NewGuid(), 10m);
        line.RecordCount(9.5m, "evaporation");
        line.Variance.Should().Be(-0.5m);
    }

    [Fact]
    public void A_transfer_line_accepts_a_fractional_quantity()
    {
        var line = new TransferOrderLine(Guid.NewGuid(), Guid.NewGuid(), 0.1m);
        line.Quantity.Should().Be(0.1m);
    }

    [Fact]
    public void Movement_and_adjustment_keep_the_fraction()
    {
        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(), 2.5m, MovementType.Received, "dock", "RM", "PO-1", "receipt");
        movement.Quantity.Should().Be(2.5m);
        var adjustment = new StockAdjustment(Guid.NewGuid(), -0.25m, AdjustmentReason.Damaged, "tester");
        adjustment.QuantityDelta.Should().Be(-0.25m);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("5.000000")]
    [InlineData("2.5")]
    public void Event_quantities_round_trip_through_Newtonsoft_and_System_Text_Json(string quantity)
    {
        var json = "{\"MovementId\":\"" + Guid.NewGuid() + "\",\"Quantity\":" + quantity + ",\"MovementType\":\"Received\"}";
        var value = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture);

        JsonConvert.DeserializeObject<StockMovementRecordedEvent>(json)!.Quantity.Should().Be(value);
        System.Text.Json.JsonSerializer.Deserialize<StockMovementRecordedEvent>(json)!.Quantity.Should().Be(value);

        var written = JsonConvert.SerializeObject(new StockMovementRecordedEvent { Quantity = value, MovementType = "Received" });
        written.Should().Contain("\"Quantity\":");
    }

    [Fact]
    public void A_whole_number_event_from_an_older_sender_is_read_as_decimal()
    {
        var alert = JsonConvert.DeserializeObject<StockAlertEvent>("{\"CurrentQuantity\":7,\"ReorderPoint\":10,\"MinStock\":10,\"MaxStock\":60}")!;
        alert.CurrentQuantity.Should().Be(7m);
        alert.MaxStock.Should().Be(60m);
    }
}
