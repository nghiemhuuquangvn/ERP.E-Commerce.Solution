using Confluent.Kafka;
using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.PAY.Infrastructure.MessageBus
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

        /// <summary>
        /// The events of one payment (captured, refunded ...) must reach consumers in order — the ledger books the refund against the
        /// capture — so they share a Kafka key and therefore a partition. Anything else keeps a random key.
        /// </summary>
        public static string PartitionKey(object message) => message switch
        {
            Domain.Events.PaymentInitiatedEvent e => e.PaymentId.ToString(),
            Domain.Events.PaymentAuthorizedEvent e => e.PaymentId.ToString(),
            Domain.Events.PaymentProcessedEvent e => e.PaymentId.ToString(),
            Domain.Events.PaymentFailedEvent e => e.PaymentId.ToString(),
            Domain.Events.PaymentRefundedEvent e => e.PaymentId.ToString(),
            _ => Guid.NewGuid().ToString(),
        };

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
                    Key = PartitionKey(message),
                    Value = messageJson
                };

                var result = await _producer.ProduceAsync("payment-topic", kafkaMessage, cancellationToken);

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
