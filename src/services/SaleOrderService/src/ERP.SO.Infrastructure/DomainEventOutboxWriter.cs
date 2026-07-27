using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Newtonsoft.Json;

namespace ERP.SO.Infrastructure
{
    /// <summary>
    /// Stages an aggregate's pending domain events into OutboxMessages as part of the same
    /// SaveChanges call that persists the aggregate — replaces the previous pattern of
    /// command handlers calling IMessageBus.PublishAsync directly after SaveChanges (not
    /// transactional: a crash between the two steps silently dropped the event).
    /// SalesOrderEventOutboxProcessor picks these rows up and publishes to RabbitMQ.
    /// Uses the same Type-name discriminator the Settings outbox path already relies on
    /// (nameof(SettingsChangedIntegrationEvent)) so the two publish paths never cross-process
    /// each other's rows from the shared OutboxMessages table.
    /// </summary>
    public static class DomainEventOutboxWriter
    {
        public static void Stage(SalesOrderDbContext context, BaseEntity aggregate)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var content = JsonConvert.SerializeObject(domainEvent, settings);
                context.OutboxMessages.Add(new OutboxMessage(domainEvent.GetType().Name, content));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
