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

                // TypeNameHandling.All is required so the serialized JSON carries a "$type"
                // property — SharedMasterDataService's IdentityEventConsumer dispatches based
                // on that field, and every other event handler in this codebase already
                // serializes this way (see e.g. FeatureFlagService.cs). Without it, this
                // event silently never dispatches on the consumer side.
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredOnUtc = DateTime.UtcNow,
                    Type = integrationEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(integrationEvent, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })
                };

                // Actual publish to Kafka's "identity-topic" happens asynchronously via the
                // OutboxProcessor background service draining this table — no direct
                // messageBus.PublishAsync call belongs here.
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