using System.Text.Json;
using ERP.RPT.Infrastructure.EventConsumers;
using FluentAssertions;
using Xunit;

namespace ERP.RPT.UnitTests.Facts;

/// <summary>Warehouse upgrade W1: a movement quantity is read as decimal; the old integer reader stored 0 for 5.000000 without any error.</summary>
public class InventoryQuantityReadTests
{
    [Theory]
    [InlineData("{\"Quantity\":5}", 5)]
    [InlineData("{\"Quantity\":5.000000}", 5)]
    [InlineData("{\"quantity\":2.5}", 2.5)]
    [InlineData("{\"Quantity\":-0.25}", -0.25)]
    public void GetDecimal_reads_whole_and_fractional_quantities(string json, double expected)
    {
        using var doc = JsonDocument.Parse(json);
        JDoc.GetDecimal(doc.RootElement, "Quantity", "quantity").Should().Be((decimal)expected);
    }
}
