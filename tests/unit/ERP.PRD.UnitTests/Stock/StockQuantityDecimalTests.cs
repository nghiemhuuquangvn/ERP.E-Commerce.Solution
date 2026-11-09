using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.PRD.UnitTests.Stock;

/// <summary>Warehouse upgrade W1: the Product copy of the stock level keeps fractions.</summary>
public class StockQuantityDecimalTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(2.5)]
    [InlineData(0.000001)]
    public void A_synced_quantity_keeps_its_fraction(double quantity)
    {
        var product = Product.Create("Paint", "SKU-STOCK-1", ProductType.Physical);
        product.SyncStockQuantity((decimal)quantity);
        product.StockQuantity.Should().Be((decimal)quantity);
    }
}
