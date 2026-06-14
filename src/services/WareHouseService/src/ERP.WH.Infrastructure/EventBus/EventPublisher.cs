using ERP.WH.Application.Commands.Inventory;
using System.Text.Json;
using RabbitMQ.Client;

namespace ERP.WH.Infrastructure.EventBus;

public class EventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly string _exchangeName = "warehouse_exchange";

    public EventPublisher(IConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken)
    {
        using (var channel = _connection.CreateModel())
        {
            channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);

            var eventName = typeof(T).Name;
            var routingKey = $"warehouse.{eventName}";

            var message = JsonSerializer.Serialize(@event);
            var body = System.Text.Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            channel.BasicPublish(_exchangeName, routingKey, properties, body);
        }

        await Task.CompletedTask;
    }
}
