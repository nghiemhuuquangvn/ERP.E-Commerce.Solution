using ERP.PO.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ERP.PO.UnitTests.Replenishment;

/// <summary>Warehouse upgrade W1: replenishment works on decimal quantities (kg, litre).</summary>
public class DecimalReplenishmentTests
{
    [Fact]
    public void A_fractional_shortfall_is_suggested_exactly()
    {
        var need = ReplenishmentCalculator.From(available: 1.5m, onOrder: 0.5m, min: 2.5m, max: 10m, safety: 1m, reorderPoint: 0, reorderQuantity: 0);
        need.IsNeeded.Should().BeTrue();
        need.Covered.Should().Be(2m);
        need.SuggestedQuantity.Should().Be(8m);
    }

    [Fact]
    public void Covered_stock_above_the_minimum_needs_nothing()
    {
        var need = ReplenishmentCalculator.From(2.25m, 0.5m, 2.5m, 10m, 1m, 0, 0);
        need.IsNeeded.Should().BeFalse();
        need.SuggestedQuantity.Should().Be(0m);
    }

    [Fact]
    public void An_older_alert_without_a_maximum_still_works_in_decimals()
    {
        var need = ReplenishmentCalculator.From(3m, 0m, 0m, 0m, 0m, reorderPoint: 5m, reorderQuantity: 20m);
        need.Min.Should().Be(5m);
        need.Max.Should().Be(25m);
        need.SuggestedQuantity.Should().Be(22m);
    }
}
