using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>R111: Product re-sends the current price of every product (old = new) to refresh the POS cache; open orders must not be flagged by it.</summary>
public class PriceChangeFlagTests
{
    private static (SalesOrder Order, Guid ProductId) OpenOrder()
    {
        var productId = Guid.NewGuid();
        var order = SalesOrder.Create(Guid.NewGuid(), "Customer", "c@example.com", new Address("1 Street", "City", "State", "700000", "VN"),
            new List<OrderLineItem> { new(productId, "Ao thun", 2, 500m) });
        return (order, productId);
    }

    [Fact]
    public void A_real_price_change_flags_the_open_order()
    {
        var (order, productId) = OpenOrder();

        order.FlagPriceChanged(productId, 500m, 450m);

        order.HasPriceChangeWarning.Should().BeTrue();
    }

    [Fact]
    public void A_re_sent_price_with_the_same_old_and_new_value_flags_nothing()
    {
        var (order, productId) = OpenOrder();

        order.FlagPriceChanged(productId, 500m, 500m);

        order.HasPriceChangeWarning.Should().BeFalse();
    }
}
