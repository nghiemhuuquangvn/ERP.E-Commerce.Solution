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
        var messageValue = JsonConvert.SerializeObject(@event);
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