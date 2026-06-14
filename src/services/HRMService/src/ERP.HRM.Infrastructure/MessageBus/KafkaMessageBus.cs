using Confluent.Kafka;
using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.HRM.Infrastructure.MessageBus
{
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
            try
            {
                var messageJson = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                var kafkaMessage = new Message<string, string>
                {
                    Key = Guid.NewGuid().ToString(),
                    Value = messageJson
                };

                var result = await _producer.ProduceAsync("hrm-topic", kafkaMessage, cancellationToken);

                _logger.LogInformation(
                    "Message published to topic {Topic} partition {Partition} offset {Offset}",
                    result.Topic,
                    result.Partition,
                    result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to Kafka");
                throw;
            }
        }
    }
}
