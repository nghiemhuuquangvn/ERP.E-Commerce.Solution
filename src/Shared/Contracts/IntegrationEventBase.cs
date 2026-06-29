using ERP.Shared.Core.Domain;

namespace ERP.Shared.Contracts
{
    /// <summary>
    /// Base class for all integration events.
    /// Integration events are published by one service and consumed by others.
    /// Each event has a unique EventId and timestamp for tracking and deduplication.
    /// </summary>
    public abstract class IntegrationEventBase : IDomainEvent
    {
        /// <summary>
        /// Unique identifier for this event instance (for deduplication)
        /// </summary>
        public Guid EventId { get; } = Guid.NewGuid();

        /// <summary>
        /// When the event occurred
        /// </summary>
        public DateTime OccurredAt { get; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public DateTime OccurredOn => OccurredAt;

        protected IntegrationEventBase()
        {
        }

        protected IntegrationEventBase(Guid eventId, DateTime occurredAt)
        {
            // Allow setting these for deserialization from Kafka
        }
    }
}
