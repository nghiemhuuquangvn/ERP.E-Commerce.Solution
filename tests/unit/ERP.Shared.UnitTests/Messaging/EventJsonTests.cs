using ERP.Shared.Messaging;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.Shared.UnitTests.Messaging;

public class EventJsonTests
{
    private static readonly Guid Id = Guid.Parse("282c2a40-934a-4d95-a236-d8249ca0e10d");

    [Fact]
    public void ValueOfNullableGuid_ThrowsOnAGuidString_WhichIsWhyGuidValueExists()
    {
        var payload = JObject.Parse($"{{\"PurchaseOrderId\":\"{Id}\"}}");

        var act = () => payload.Value<Guid?>("PurchaseOrderId");

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void GuidValue_ReadsAGuidString()
    {
        var payload = JObject.Parse($"{{\"PurchaseOrderId\":\"{Id}\"}}");

        payload.GuidValue("PurchaseOrderId").Should().Be(Id);
    }

    [Fact]
    public void GuidValue_ReturnsNull_WhenMissingNullOrNotAGuid()
    {
        var payload = JObject.Parse("{\"A\":null,\"B\":\"not-a-guid\",\"C\":5}");

        payload.GuidValue("A").Should().BeNull();
        payload.GuidValue("B").Should().BeNull();
        payload.GuidValue("C").Should().BeNull();
        payload.GuidValue("Missing").Should().BeNull();
        ((JToken?)null).GuidValue("A").Should().BeNull();
    }

    [Fact]
    public void GuidValue_ReadsAGuidTokenCreatedInCode()
    {
        var payload = new JObject { ["Id"] = new JValue(Id) };

        payload.GuidValue("Id").Should().Be(Id);
    }
}
