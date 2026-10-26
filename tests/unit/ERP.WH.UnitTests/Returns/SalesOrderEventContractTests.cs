using System.Text.Json;
using ERP.WH.Application.EventHandlers;
using FluentAssertions;
using Xunit;

namespace ERP.WH.UnitTests.Returns;

/// <summary>
/// ROADMAP P1-5 (found in the live check): the Warehouse copies of Sales' cancelled / shipped events required a CorrelationId that Sales never sends, so System.Text.Json refused
/// every message and the reservation of a cancelled order was never released (nor the stock of a shipped one issued). They must read what Sales really publishes.
/// </summary>
public class SalesOrderEventContractTests
{
    private const string Tenant = "0b92b259-6933-483b-84e7-42533c846f8c";

    [Fact]
    public void A_cancelled_order_event_as_Sales_publishes_it_is_read()
    {
        var orderId = Guid.NewGuid();
        var json = $"{{\"TenantId\":\"{Tenant}\",\"OrderId\":\"{orderId}\",\"CustomerId\":\"{Guid.NewGuid()}\",\"CancellationReason\":\"Payment failed\",\"CancelledAt\":\"2026-10-26T03:00:00Z\",\"EventId\":\"{Guid.NewGuid()}\",\"OccurredAt\":\"2026-10-26T03:00:00Z\"}}";

        var evt = JsonSerializer.Deserialize<SalesOrderCancelledEvent>(json)!;

        (evt.OrderId, evt.CancellationReason).Should().Be((orderId, "Payment failed"));
    }

    [Fact]
    public void A_shipped_order_event_as_Sales_publishes_it_is_read()
    {
        var orderId = Guid.NewGuid();
        var json = $"{{\"TenantId\":\"{Tenant}\",\"OrderId\":\"{orderId}\",\"CustomerId\":\"{Guid.NewGuid()}\",\"TrackingNumber\":\"TRK-1\",\"Carrier\":\"DHL\",\"ShippedAt\":\"2026-10-26T03:00:00Z\"}}";

        var evt = JsonSerializer.Deserialize<SalesOrderShippedEvent>(json)!;

        (evt.OrderId, evt.TrackingNumber).Should().Be((orderId, "TRK-1"));
    }
}
