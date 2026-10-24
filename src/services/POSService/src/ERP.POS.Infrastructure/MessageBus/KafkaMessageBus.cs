using Confluent.Kafka;
using ERP.POS.Domain.Events;
using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.POS.Infrastructure.MessageBus;

public class KafkaMessageBus : IMessageBus
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessageBus> _logger;

    public KafkaMessageBus(IProducer<string, string> producer, ILogger<KafkaMessageBus> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class
    {
        var messageJson = JsonConvert.SerializeObject(message, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

        var kafkaMessage = new Message<string, string> { Key = PartitionKey(message), Value = messageJson };
        var result = await _producer.ProduceAsync("pos-topic", kafkaMessage, cancellationToken);

        _logger.LogInformation("Message published to topic {Topic} partition {Partition} offset {Offset}",
            result.Topic, result.Partition, result.Offset);
    }

    /// <summary>
    /// A sale and its void must reach consumers in that order (the ledger reverses the sale's entry), so they share one Kafka key and
    /// therefore one partition. Anything else keeps a random key.
    /// </summary>
    public static string PartitionKey(object message) => message switch
    {
        TransactionCreatedEvent e => e.TransactionId.ToString(),
        TransactionVoidedEvent e => e.TransactionId.ToString(),
        CashShiftClosedEvent e => e.ShiftId.ToString(),
        _ => Guid.NewGuid().ToString(),
    };
}
