using Confluent.Kafka;
using ERP.Identity.Application.Interfaces;
using ERP.Identity.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ERP.Identity.Infrastructure.MessageBus;

public class KafkaMessageBus : IMessageBus
{
    private readonly IProducer<string, string> _producer;
    //private readonly IConsumer<string, string> _consumer;

    public KafkaMessageBus(IProducer<string, string> producer)
    {
        _producer = producer;
        //_consumer = consumer;
    }

    public async Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken cancellationToken = default)
    {
        // The Outbox stores each event already serialized (with its $type) and passes that string through: serializing
        // it again wrapped the JSON in a JSON string, which no consumer could parse ("Current JsonReader item is not an
        // object: String") - so no Identity event (2FA code, password reset, email confirmation, UserCreated ...) was
        // ever delivered.
        var messageValue = @event is string alreadySerialized ? alreadySerialized : JsonConvert.SerializeObject(@event);
        var kafkaMessage = new Message<string, string> { Value = messageValue };
        
        await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
    }

    public Task SubscribeAsync<TEvent>(string topic, Func<TEvent, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        // _consumer.Subscribe(topic);

        // Task.Run(async () =>
        // {
        //     while (!cancellationToken.IsCancellationRequested)
        //     {
        //         var consumeResult = _consumer.Consume(cancellationToken);
        //         if (consumeResult != null)
        //         {
        //             var @event = System.Text.Json.JsonSerializer.Deserialize<TEvent>(consumeResult.Message.Value);
        //             if (@event != null)
        //             {
        //                 await handler(@event, cancellationToken);
        //             }
        //         }
        //     }
        // }, cancellationToken);

        return Task.CompletedTask;
    }
}