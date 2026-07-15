namespace ERP.SO.Application.Interfaces;

/// <summary>
/// Isolated publish path for Settings-changed integration events only. Deliberately a
/// separate interface from <c>ERP.Shared.Messaging.IMessageBus</c> (used by
/// <c>RabbitMqMessageBus</c> for real domain events like OrderCreated) so this new Kafka
/// producer path cannot create ambiguous DI resolution or otherwise regress the existing
/// RabbitMQ order-fulfillment event flow.
/// </summary>
public interface ISettingsEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
