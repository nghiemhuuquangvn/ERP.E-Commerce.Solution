namespace ERP.Identity.infrastructure.Events.Handlers
{
    using ERP.Identity.Application.Interfaces;
    using ERP.Identity.Application.Interfaces.Repositories;
    using ERP.Identity.Domain.Events;
    using ERP.Identity.Infrastructure.MessageBus.IntegrationEvents;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class UserRegisteredDomainEventHandler : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        private readonly ILogger<UserRegisteredDomainEventHandler> _logger;
        private readonly IOutboxMessageRepository _outboxMessageRepository;

        public UserRegisteredDomainEventHandler(ILogger<UserRegisteredDomainEventHandler> logger, IOutboxMessageRepository outboxMessageRepository)
        {
            _logger = logger;
            _outboxMessageRepository = outboxMessageRepository;
        }

        public async Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Handling UserRegisteredDomainEvent for UserId: {UserId}", domainEvent.UserId);
                
                // Create the integration event
                var integrationEvent = new UserRegisteredIntegrationEvent(
                    userId: domainEvent.UserId,
                    tenantId: domainEvent.TenantId,
                    email: domainEvent.Email,
                    userType: domainEvent.UserType
                    
                );

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredOnUtc = DateTime.UtcNow,
                    Type = integrationEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(integrationEvent)
                };
                

                // Publish the integration event to the message bus
                //await _messageBus.PublishAsync("user-registered-topic", integrationEvent, cancellationToken);
                await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling UserRegisteredDomainEvent for UserId: {UserId}", domainEvent.UserId);
                throw;
            }
        }
    }
}