using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.Shared.Messaging
{
    /// <summary>
    /// Shared rules every OutboxProcessor uses to turn an <see cref="OutboxMessage"/> back into
    /// the event object it was written from.
    ///
    /// Resolution goes by the <c>$type</c> embedded in <see cref="OutboxMessage.Content"/>
    /// (all writers serialize with <c>TypeNameHandling.All</c>), not by the <see cref="OutboxMessage.Type"/>
    /// column: that column is inconsistent across services (bare Name, FullName or
    /// AssemblyQualifiedName), and resolving a bare name via <c>Type.GetType</c> returns null —
    /// which used to make processors silently drop e.g. every <c>SettingsChangedIntegrationEvent</c>.
    /// </summary>
    public static class OutboxEventDeserializer
    {
        /// <summary>Maximum number of messages a processor loads per polling cycle.</summary>
        public const int DefaultBatchSize = 100;

        /// <summary>
        /// Prefix written to <see cref="OutboxMessage.Error"/> for messages that can never be
        /// deserialized. Such messages keep <c>ProcessedOnUtc = null</c> (they were never
        /// published) but are excluded from polling; clearing <c>Error</c> re-queues them.
        /// </summary>
        public const string DeadLetterPrefix = "[DeadLetter] ";

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public static bool IsDeadLettered(OutboxMessage message) =>
            message.Error != null && message.Error.StartsWith(DeadLetterPrefix, StringComparison.Ordinal);

        public static void MarkDeadLettered(OutboxMessage message, string reason) =>
            message.Error = DeadLetterPrefix + reason;

        /// <summary>
        /// Deserializes <paramref name="message"/>'s content. Returns false (with a reason in
        /// <paramref name="error"/>) when the content is malformed or names a type that can't be
        /// resolved — i.e. retrying will never succeed and the message should be dead-lettered.
        /// </summary>
        public static bool TryDeserialize(OutboxMessage message, out object? @event, out string? error)
        {
            @event = null;
            error = null;

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                error = "Empty content";
                return false;
            }

            try
            {
                var hasEmbeddedType = JToken.Parse(message.Content) is JObject obj && obj["$type"] != null;

                if (hasEmbeddedType)
                {
                    @event = JsonConvert.DeserializeObject(message.Content, SerializerSettings);
                }
                else
                {
                    var eventType = string.IsNullOrWhiteSpace(message.Type) ? null : Type.GetType(message.Type);
                    if (eventType == null)
                    {
                        error = $"Event type not resolvable: {message.Type}";
                        return false;
                    }

                    @event = JsonConvert.DeserializeObject(message.Content, eventType, SerializerSettings);
                }
            }
            catch (JsonException ex)
            {
                error = $"Deserialization failed for {message.Type}: {ex.Message}";
                return false;
            }

            if (@event == null || @event is JToken)
            {
                error = $"Deserialization failed for {message.Type}";
                @event = null;
                return false;
            }

            return true;
        }
    }
}
