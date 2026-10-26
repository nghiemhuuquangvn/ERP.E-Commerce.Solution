using ERP.RPT.Infrastructure.EventConsumers;
using FluentAssertions;
using Xunit;

namespace ERP.RPT.UnitTests.Facts;

/// <summary>ROADMAP P1-6: Reporting tells a partial refund from a full one, counts a replayed refund once, and has no dead consumer.</summary>
public class ReportingFactsTests
{
    [Theory]
    [InlineData(1000, 0, "Processed")]
    [InlineData(1000, 300, "PartiallyRefunded")]
    [InlineData(1000, 999.99, "PartiallyRefunded")]
    [InlineData(1000, 1000, "Refunded")]
    [InlineData(1000, 1200, "Refunded")]
    public void The_status_follows_how_much_came_back(double paid, double refunded, string status) =>
        ReportingRefunds.StatusFor((decimal)paid, (decimal)refunded).Should().Be(status);

    [Fact]
    public void A_refund_without_an_id_gets_the_same_id_every_time_so_a_replay_counts_once()
    {
        var payment = Guid.NewGuid();

        ReportingRefunds.DeterministicId(payment, 50m).Should().Be(ReportingRefunds.DeterministicId(payment, 50m));
        ReportingRefunds.DeterministicId(payment, 50m).Should().NotBe(ReportingRefunds.DeterministicId(payment, 30m));
        ReportingRefunds.DeterministicId(payment, 50m).Should().NotBe(ReportingRefunds.DeterministicId(Guid.NewGuid(), 50m));
    }

    [Fact]
    public void The_dead_sales_order_kafka_consumer_is_gone_and_the_crm_consumer_exists()
    {
        var types = typeof(ReportingRefunds).Assembly.GetTypes().Select(t => t.Name).ToList();

        types.Should().NotContain("SalesOrderEventConsumer");   // listened to sales-order-* topics nobody writes
        types.Should().Contain("CrmEventConsumer");
    }
}
