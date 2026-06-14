namespace ERP.Shared.Messaging
{
    /// <summary>
    /// Outbox message entity for the Outbox pattern.
    /// Guarantees that domain events are reliably published to the message bus.
    /// Events are stored in the Outbox table within the same DB transaction as entity changes.
    /// A background job (OutboxProcessor) polls this table and publishes to Kafka.
    /// </summary>
    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Fully qualified type name of the event class for deserialization
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Serialized event content (JSON)
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// When the event occurred
        /// </summary>
        public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the event was published to Kafka (null if not yet published)
        /// </summary>
        public DateTime? ProcessedOnUtc { get; set; }

        /// <summary>
        /// Error message if publishing failed
        /// </summary>
        public string? Error { get; set; }

        public OutboxMessage() { }

        public OutboxMessage(string type, string content)
        {
            Type = type;
            Content = content;
            OccurredOnUtc = DateTime.UtcNow;
        }
    }
}
