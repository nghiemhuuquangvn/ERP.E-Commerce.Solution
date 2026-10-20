using ERP.Shared.Contracts;
using ERP.Shared.Messaging;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace ERP.Shared.UnitTests.Messaging;

public class OutboxEventDeserializerTests
{
    private static readonly JsonSerializerSettings TypedSettings = new() { TypeNameHandling = TypeNameHandling.All };

    private static SettingsChangedIntegrationEvent NewSettingsEvent() => new()
    {
        ServiceName = "PaymentService",
        Category = "Payment",
        Scope = "System"
    };

    [Fact]
    public void IntegrationEvent_RoundTrip_PreservesEventIdAndOccurredAt()
    {
        var original = NewSettingsEvent();

        var json = JsonConvert.SerializeObject(original, TypedSettings);
        var restored = JsonConvert.DeserializeObject<SettingsChangedIntegrationEvent>(json, TypedSettings)!;

        restored.EventId.Should().Be(original.EventId);
        restored.OccurredAt.Should().Be(original.OccurredAt);
    }

    [Fact]
    public void TryDeserialize_BareTypeName_ResolvesFromEmbeddedTypeAndKeepsEventId()
    {
        // *SettingsService writers store Type = nameof(...) — Type.GetType() can't resolve that.
        var original = NewSettingsEvent();
        var message = new OutboxMessage(nameof(SettingsChangedIntegrationEvent),
            JsonConvert.SerializeObject(original, TypedSettings));

        var ok = OutboxEventDeserializer.TryDeserialize(message, out var @event, out var error);

        ok.Should().BeTrue(error);
        var restored = @event.Should().BeOfType<SettingsChangedIntegrationEvent>().Subject;
        restored.EventId.Should().Be(original.EventId);
        restored.ServiceName.Should().Be("PaymentService");
    }

    [Fact]
    public void TryDeserialize_ContentWithoutEmbeddedType_FallsBackToTypeColumn()
    {
        var original = NewSettingsEvent();
        var message = new OutboxMessage(typeof(SettingsChangedIntegrationEvent).AssemblyQualifiedName!,
            JsonConvert.SerializeObject(original));

        var ok = OutboxEventDeserializer.TryDeserialize(message, out var @event, out _);

        ok.Should().BeTrue();
        @event.Should().BeOfType<SettingsChangedIntegrationEvent>()
            .Which.EventId.Should().Be(original.EventId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("{\"$type\":\"Does.Not.Exist, Nowhere\",\"Value\":1}")]
    [InlineData("{\"Value\":1}")]
    public void TryDeserialize_UnrecoverableContent_ReturnsFalseWithReason(string content)
    {
        var message = new OutboxMessage("UnknownEvent", content);

        var ok = OutboxEventDeserializer.TryDeserialize(message, out var @event, out var error);

        ok.Should().BeFalse();
        @event.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MarkDeadLettered_LeavesMessageUnprocessedButFlagged()
    {
        var message = new OutboxMessage("X", "{}");

        OutboxEventDeserializer.MarkDeadLettered(message, "boom");

        message.ProcessedOnUtc.Should().BeNull();
        OutboxEventDeserializer.IsDeadLettered(message).Should().BeTrue();
        message.Error.Should().Be(OutboxEventDeserializer.DeadLetterPrefix + "boom");
    }
}
