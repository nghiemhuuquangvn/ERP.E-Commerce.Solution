using ERP.Shared.Contracts;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace ERP.Shared.UnitTests.Messaging;

public class EventTenantTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private sealed class OrderPlaced : IntegrationEventBase, ITenantEvent
    {
        public Guid OrderId { get; set; }
        public Guid TenantId { get; set; }
    }

    private sealed class LocalOnly : IntegrationEventBase
    {
    }

    [Fact]
    public void Stamp_SetsTenant_AndSurvivesTheOutboxRoundTrip()
    {
        var evt = new OrderPlaced { OrderId = Guid.NewGuid() };

        EventTenant.Stamp(evt, Tenant);
        var json = JsonConvert.SerializeObject(evt, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

        EventTenant.TryRead(json, out var fromJson).Should().BeTrue();
        fromJson.Should().Be(Tenant);
        var restored = JsonConvert.DeserializeObject(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        EventTenant.TryGet(restored, out var fromObject).Should().BeTrue();
        fromObject.Should().Be(Tenant);
    }

    [Fact]
    public void Stamp_KeepsATenantTheEventAlreadyCarries()
    {
        var own = Guid.NewGuid();
        var evt = new OrderPlaced { TenantId = own };

        EventTenant.Stamp(evt, Tenant);

        evt.TenantId.Should().Be(own);
    }

    [Fact]
    public void Stamp_IgnoresEventsThatDontCarryATenant()
    {
        var act = () => EventTenant.Stamp(new LocalOnly(), Tenant);

        act.Should().NotThrow();
        EventTenant.TryGet(new LocalOnly(), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"OrderId\":\"6b1c5f8e-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"TenantId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"TenantId\":null}")]
    public void TryRead_WithoutAUsableTenant_IsFalse(string? json)
    {
        EventTenant.TryRead(json, out _).Should().BeFalse();
    }

    [Fact]
    public void TryRead_IsCaseInsensitive()
    {
        EventTenant.TryRead($"{{\"tenantId\":\"{Tenant}\"}}", out var tenant).Should().BeTrue();
        tenant.Should().Be(Tenant);
    }
}
