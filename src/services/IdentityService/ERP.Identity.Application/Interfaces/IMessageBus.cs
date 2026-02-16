namespace ERP.Identity.Application.Interfaces
{
    public interface IMessageBus
    {
        Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken cancellationToken = default);
        Task SubscribeAsync<TEvent>(string topic, Func<TEvent, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
    }
}