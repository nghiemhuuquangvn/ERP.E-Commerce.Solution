namespace ERP.Identity.infrastructure.Events.Handlers
{
    using ERP.Identity.Application.Interfaces;
    using ERP.Identity.Domain.Events;

    public class UserRegisteredDomainEventHandler : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        private readonly IMessageBus _messageBus;

        public UserRegisteredDomainEventHandler(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public async Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            // Publish the event to the message bus
            await _messageBus.PublishAsync("user-registered-topic", domainEvent, cancellationToken);
        }
    }
}