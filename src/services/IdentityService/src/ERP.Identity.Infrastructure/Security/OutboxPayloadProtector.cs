using System.IO;
using ERP.Shared.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.Identity.Infrastructure.Security
{
    /// <summary>
    /// Keeps single-use secrets out of <c>OutboxMessages.Content</c>. The three events below carry a 2FA code or a
    /// password-reset / email-confirmation link in plaintext; the outbox row used to keep them for ever (processed rows
    /// are never deleted) and so did the Kafka topic. Now the secret field is encrypted when the row is written
    /// (<see cref="Seal"/>; NotificationService opens it with the same key) and replaced by
    /// <see cref="SecretEnvelope.Redacted"/> as soon as the message has been published (<see cref="Redact"/>).
    /// Every other field (<c>$type</c>, TenantId, UserId, Method, Destination …) stays readable, so consumers that only
    /// need those are unaffected.
    /// </summary>
    public class OutboxPayloadProtector
    {
        private static readonly Dictionary<string, string[]> SecretFields = new(StringComparer.OrdinalIgnoreCase)
        {
            ["TwoFactorCodeGeneratedDomainEvent"] = new[] { "Code" },
            ["PasswordResetRequestedDomainEvent"] = new[] { "ResetLink" },
            ["EmailConfirmationRequestedDomainEvent"] = new[] { "ConfirmLink" },
        };

        private readonly ISecretProtector _protector;

        public OutboxPayloadProtector(ISecretProtector protector)
        {
            _protector = protector;
        }

        public static bool CarriesSecret(string? eventType) => eventType != null && SecretFields.ContainsKey(eventType);

        /// <summary>Encrypts the secret fields of <paramref name="json"/> (no-op for other event types).</summary>
        public string Seal(string eventType, string json) =>
            Rewrite(eventType, json, value => SecretEnvelope.Seal(_protector, value));

        /// <summary>Overwrites the secret fields with a placeholder (no-op for other event types).</summary>
        public static string Redact(string? eventType, string json) =>
            eventType != null && CarriesSecret(eventType) ? Rewrite(eventType, json, _ => SecretEnvelope.Redacted) : json;

        private static string Rewrite(string eventType, string json, Func<string, string> transform)
        {
            if (!SecretFields.TryGetValue(eventType, out var fields))
                return json;

            // Dates stay strings: parsing them to DateTime would re-format OccurredOn for every consumer.
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            var payload = JObject.Load(reader);
            foreach (var field in fields)
            {
                if (payload[field] is JValue { Type: JTokenType.String } value && value.Value is string text)
                    payload[field] = transform(text);
            }

            return payload.ToString(Formatting.None);
        }
    }
}
