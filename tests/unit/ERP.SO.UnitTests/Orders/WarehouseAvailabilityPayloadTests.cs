using System.Net;
using ERP.SO.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>
/// Warehouse upgrade W1: the Warehouse now answers decimal quantities ("availableQuantity":5.000000). The client is fail-closed, so an int-only reader
/// would have rejected every order; it must read both shapes.
/// </summary>
public class WarehouseAvailabilityPayloadTests
{
    private sealed class Reply(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private static WarehouseServiceClient Client(string body)
        => new(new HttpClient(new Reply(body)), new ConfigurationBuilder().Build(), NullLogger<WarehouseServiceClient>.Instance);

    [Theory]
    [InlineData("{\"availableQuantity\":5}", 3, true)]
    [InlineData("{\"availableQuantity\":5.000000}", 5, true)]
    [InlineData("{\"availableQuantity\":2.5}", 3, false)]
    [InlineData("{\"availableQuantity\":2.5}", 2.5, true)]
    public async Task Availability_is_read_as_decimal(string body, double requested, bool expectedAvailable)
    {
        var result = await Client(body).CheckAvailabilityAsync(Guid.NewGuid(), (decimal)requested, CancellationToken.None);
        result.IsAvailable.Should().Be(expectedAvailable);
    }
}
