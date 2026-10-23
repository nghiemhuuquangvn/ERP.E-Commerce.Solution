using Confluent.Kafka;
using ERP.Identity.Infrastructure.MessageBus;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.Identity.UnitTests.Infrastructure;

public class KafkaMessageBusTests
{
    private static (KafkaMessageBus Bus, Func<string?> Sent) Create()
    {
        string? sent = null;
        var producer = new Mock<IProducer<string, string>>();
        producer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => sent = message.Value)
            .ReturnsAsync(new DeliveryResult<string, string>());
        return (new KafkaMessageBus(producer.Object), () => sent);
    }

    [Fact]
    public async Task PublishAsync_SendsAnOutboxPayloadAsItIs_NotWrappedInAJsonString()
    {
        const string content = "{\"$type\":\"X, Y\",\"Destination\":\"admin@acme.test\",\"Code\":\"123456\"}";
        var (bus, sent) = Create();

        await bus.PublishAsync("identity-topic", content);

        sent().Should().Be(content);
        JToken.Parse(sent()!).Type.Should().Be(JTokenType.Object, "a consumer does JObject.Parse(message)");
    }

    [Fact]
    public async Task PublishAsync_StillSerializesAnObject()
    {
        var (bus, sent) = Create();

        await bus.PublishAsync("identity-topic", new { Name = "x" });

        JObject.Parse(sent()!)["Name"]!.Value<string>().Should().Be("x");
    }
}
