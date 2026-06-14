using Confluent.Kafka;
using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.PRD.Infrastructure
{
    /// <summary>
    /// Kafka-based implementation of the message bus.
    /// Publishes integration events to Kafka topics for consumption by other services.
    /// </summary>
    public class KafkaMessageBus : IMessageBus
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaMessageBus> _logger;
        private const string TopicName = "product-topic";

        public KafkaMessageBus(IProducer<string, string> producer, ILogger<KafkaMessageBus> logger)
        {
            _producer = producer;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var serialized = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    NullValueHandling = NullValueHandling.Ignore
                });

                var kafkaMessage = new Message<string, string>
                {
                    Key = Guid.NewGuid().ToString(),
                    Value = serialized
                };

                var deliveryResult = await _producer.ProduceAsync(TopicName, kafkaMessage, cancellationToken);

                _logger.LogInformation(
                    "Published event {MessageType} to {Topic}:{Partition}@{Offset}",
                    typeof(T).Name,
                    deliveryResult.Topic,
                    deliveryResult.Partition,
                    deliveryResult.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message of type {MessageType}", typeof(T).Name);
                throw;
            }
        }
    }
}
