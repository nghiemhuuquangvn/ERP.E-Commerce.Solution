namespace ERP.Shared.Messaging
{
    /// <summary>
    /// Generic event handler interface for consuming integration events.
    /// Implementations handle specific event types published by other services.
    /// </summary>
    public interface IEventHandler<in TEvent> where TEvent : class
    {
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    }
}
