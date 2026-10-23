using Newtonsoft.Json.Linq;

namespace ERP.Shared.Messaging
{
    /// <summary>
    /// Typed reads from an integration-event payload parsed with <c>JObject.Parse</c>.
    /// </summary>
    public static class EventJson
    {
        /// <summary>
        /// Reads a GUID property. <c>JToken.Value&lt;Guid?&gt;(name)</c> must not be used for this:
        /// Newtonsoft keeps a GUID as a JSON string and converts with <c>Convert.ChangeType</c>,
        /// which throws "Invalid cast from 'System.String' to 'System.Guid'" — the whole message
        /// is then lost. Returns null when the property is missing, null or not a GUID.
        /// </summary>
        public static JArray? ArrayValue(this JToken? payload, string propertyName)
        {
            // Outbox writers serialize with TypeNameHandling.All, which turns a list into
            // {"$type": "...List`1...", "$values": [ ... ]}; a plain `token as JArray` is then null and the
            // collection is silently treated as empty. Plain arrays (other publishers) are returned as they are.
            return payload?[propertyName] switch
            {
                JArray array => array,
                JObject wrapper when wrapper["$values"] is JArray values => values,
                _ => null,
            };
        }

        public static Guid? GuidValue(this JToken? payload, string propertyName)
        {
            var token = payload?[propertyName];
            if (token is null || token.Type == JTokenType.Null)
                return null;

            if (token.Type == JTokenType.Guid)
                return token.Value<Guid>();

            return Guid.TryParse(token.Type == JTokenType.String ? token.Value<string>() : token.ToString(), out var value)
                ? value
                : null;
        }
    }
}
