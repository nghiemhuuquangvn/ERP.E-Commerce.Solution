namespace ERP.Shared.Messaging
{
    /// <summary>
    /// Message bus interface for publishing integration events.
    /// Implementations handle Kafka or other message broker publishing.
    /// </summary>
    public interface IMessageBus
    {
        Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
    }
}
