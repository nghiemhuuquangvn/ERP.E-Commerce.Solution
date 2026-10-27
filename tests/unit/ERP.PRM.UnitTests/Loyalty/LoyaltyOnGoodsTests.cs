using ERP.PRM.Infrastructure.EventBus;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.PRM.UnitTests.Loyalty;

/// <summary>ROADMAP P1-11b: loyalty points are earned on the goods, not on the delivery fee the customer also paid.</summary>
public class LoyaltyOnGoodsTests
{
    [Fact]
    public void The_delivery_fee_is_taken_out_of_the_total() =>
        SalesOrderRabbitMqConsumer.GoodsTotal(JObject.Parse("{\"TotalAmount\":230000,\"ShippingFee\":30000}")).Should().Be(200_000m);

    [Fact]
    public void An_event_from_before_the_fee_existed_counts_all_of_it() =>
        SalesOrderRabbitMqConsumer.GoodsTotal(JObject.Parse("{\"TotalAmount\":200000}")).Should().Be(200_000m);

    [Fact]
    public void The_goods_never_go_below_zero() =>
        SalesOrderRabbitMqConsumer.GoodsTotal(JObject.Parse("{\"TotalAmount\":10,\"ShippingFee\":30000}")).Should().Be(0m);
}
