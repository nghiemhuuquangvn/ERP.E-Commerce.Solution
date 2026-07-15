using Confluent.Kafka;
using ERP.SO.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.SO.Infrastructure.MessageBus
{
    /// <summary>
    /// Kafka producer for Settings-changed integration events only. A separate, isolated
    /// path from <see cref="ERP.SO.Infrastructure.RabbitMqMessageBus"/> — does not
    /// implement <c>ERP.Shared.Messaging.IMessageBus</c> and does not touch the
    /// "sales_order_events" RabbitMQ exchange or the order-fulfillment saga in any way.
    /// Mirrors ProductService's KafkaMessageBus internal shape (Newtonsoft.Json,
    /// TypeNameHandling.All) but publishes to "sale-order-settings-topic" via
    /// <see cref="ISettingsEventPublisher"/>.
    /// </summary>
    public class KafkaSettingsEventPublisher : ISettingsEventPublisher
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaSettingsEventPublisher> _logger;
        private const string TopicName = "sale-order-settings-topic";

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
                    "Published settings event {MessageType} to {Topic}:{Partition}@{Offset}",
                    typeof(T).Name,
                    deliveryResult.Topic,
                    deliveryResult.Partition,
                    deliveryResult.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing settings event of type {MessageType}", typeof(T).Name);
                throw;
            }
        }
    }
}
