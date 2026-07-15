namespace ERP.WH.Application.Interfaces;

/// <summary>
/// Isolated publish path used only for Settings-changed integration events. Deliberately
/// separate from <see cref="IEventPublisher"/> (RabbitMQ, warehouse_exchange, inventory
/// domain events) so this new Kafka-based flow can never regress existing inventory
/// reservation event publishing.
/// </summary>
public interface ISettingsEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
