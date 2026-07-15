using Confluent.Kafka;
using ERP.WH.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.WH.Infrastructure.MessageBus
{
    /// <summary>
    /// Kafka-based implementation used only for Settings-changed integration events.
    /// Entirely isolated from IEventPublisher/EventPublisher (RabbitMQ, warehouse_exchange)
    /// which continues to carry real inventory domain events unchanged. Mirrors
    /// ProductService's KafkaMessageBus shape.
    /// </summary>
    public class KafkaSettingsEventPublisher : ISettingsEventPublisher
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaSettingsEventPublisher> _logger;
        private const string TopicName = "warehouse-settings-topic";

        public KafkaSettingsEventPublisher(IProducer<string, string> producer, ILogger<KafkaSettingsEventPublisher> logger)
        {
            _producer = producer;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
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

                var deliveryResult = await _producer.ProduceAsync(TopicName, kafkaMessage, ct);

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
