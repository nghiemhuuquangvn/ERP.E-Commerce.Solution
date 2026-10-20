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
        /// Unique identifier for this event instance (for deduplication).
        /// <c>init</c> (not get-only) so JSON deserializers can restore the original value —
        /// with a get-only property every Outbox re-read / Kafka consume produced a new Guid,
        /// which made EventId useless as a dedup key.
        /// </summary>
        public Guid EventId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// When the event occurred. <c>init</c> for the same round-trip reason as <see cref="EventId"/>.
        /// </summary>
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public DateTime OccurredOn => OccurredAt;

        protected IntegrationEventBase()
        {
        }

        protected IntegrationEventBase(Guid eventId, DateTime occurredAt)
        {
            EventId = eventId;
            OccurredAt = occurredAt;
        }
    }
}
