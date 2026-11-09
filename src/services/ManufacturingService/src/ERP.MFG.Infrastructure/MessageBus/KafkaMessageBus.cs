using Confluent.Kafka;
using ERP.MFG.Domain.Events;
using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.MFG.Infrastructure.MessageBus;

/// <summary>Publishes Manufacturing's integration events on Kafka topic <c>manufacturing-topic</c>.</summary>
public sealed class KafkaMessageBus(IProducer<string, string> producer, ILogger<KafkaMessageBus> logger) : IMessageBus
{
    public const string Topic = "manufacturing-topic";

    /// <summary>The events of one order share a key, so they stay in order on one partition.</summary>
    public static string PartitionKey(object message) => message switch
    {
        ProductionOrderCreatedEvent e => e.OrderId.ToString(),
        ProductionOrderStatusChangedEvent e => e.OrderId.ToString(),
        _ => Guid.NewGuid().ToString(),
    };

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var json = JsonConvert.SerializeObject(message, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            var result = await producer.ProduceAsync(Topic, new Message<string, string> { Key = PartitionKey(message), Value = json }, cancellationToken);
            logger.LogInformation("Message published to topic {Topic} partition {Partition} offset {Offset}", result.Topic, result.Partition, result.Offset);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing message to Kafka");
            throw;
        }
    }
}
