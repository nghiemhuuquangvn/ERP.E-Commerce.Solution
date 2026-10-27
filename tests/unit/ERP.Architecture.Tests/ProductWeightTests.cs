using ERP.PRD.Domain.Weights;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>ROADMAP P1-11b: a variant's weight reaches the carrier in grams, whatever unit the catalog holds it in.</summary>
public class ProductWeightTests
{
    [Theory]
    [InlineData(1.5, "kg", 1500)]
    [InlineData(1.5, null, 1500)]          // kg is the catalog default
    [InlineData(250, "g", 250)]
    [InlineData(2, "lb", 907)]
    [InlineData(10, "oz", 283)]
    [InlineData(0.0004, "kg", 1)]           // a tiny positive weight is never rounded to "no weight"
    public void A_weight_is_converted_to_grams(double weight, string? unit, int expected) =>
        WeightConverter.ToGrams((decimal)weight, unit).Should().Be(expected);

    [Fact]
    public void No_usable_weight_is_reported_as_none()
    {
        WeightConverter.ToGrams(null, "kg").Should().BeNull();
        WeightConverter.ToGrams(0m, "kg").Should().BeNull();
        WeightConverter.ToGrams(-3m, "kg").Should().BeNull();
        WeightConverter.ToGrams(5m, "stone").Should().BeNull();   // an unknown unit is not guessed
    }
}
