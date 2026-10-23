using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A tenant-owned HTTP callback subscription for domain events an external system
    /// (no Kafka access) wants delivered — see docs/business/Identity/CommonServiceRoadmap.md
    /// §3. Delivery is fire-and-forget from OutboxProcessor (mirrors NotificationService's
    /// WebhookSender/WebhookEndpoint pattern — same HMAC-SHA256 signing scheme, no separate
    /// persisted delivery/retry queue).
    /// </summary>
    public class WebhookEndpoint
    {
        public Guid Id { get; private set; }
        public Guid TenantId { get; private set; }
        public string Name { get; private set; } = default!;
        public string Url { get; private set; } = default!;

        /// <summary>Used to HMAC-SHA256-sign each delivered payload (X-Hub-Signature header)
        /// so the receiver can verify authenticity — null means unsigned deliveries. Stored ENCRYPTED
        /// (<c>enc:v1:</c>, see SecretEnvelope); rows written before that hold the plaintext until the startup job
        /// re-encrypts them.</summary>
        public string? Secret { get; private set; }

        public bool IsActive { get; private set; }

        /// <summary>Comma-separated domain event type names (e.g.
        /// "UserRegisteredDomainEvent,UserUpdatedDomainEvent") this endpoint wants —
        /// empty means every eligible event type. TwoFactorCodeGeneratedDomainEvent is never
        /// eligible regardless of this filter (see OutboxProcessor) — it carries a plaintext
        /// OTP that must never reach an external URL.</summary>
        public string EventTypesRaw { get; private set; } = string.Empty;

        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        public IReadOnlyList<string> EventTypes =>
            EventTypesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .ToList();

        private WebhookEndpoint() { }

        public static WebhookEndpoint Create(Guid tenantId, string name, string url, string? secret, IEnumerable<string> eventTypes)
        {
            return new WebhookEndpoint
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Url = url,
                Secret = secret,
                IsActive = true,
                EventTypesRaw = string.Join(",", eventTypes),
                CreatedAt = DateTime.UtcNow,
            };
        }

        public void Update(string name, string url, string? secret, IEnumerable<string> eventTypes)
        {
            Name = name;
            Url = url;
            // The secret is never shown back to the client, so a form that is saved again without one must not wipe it.
            if (!string.IsNullOrEmpty(secret))
                Secret = secret;
            EventTypesRaw = string.Join(",", eventTypes);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Replaces the stored form of the secret (encrypting it) without touching anything else.</summary>
        public void SetStoredSecret(string? secret) => Secret = secret;

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>True if this endpoint should receive the given event type — an empty
        /// filter (EventTypesRaw) means "every event type".</summary>
        public bool Matches(string eventType)
        {
            var types = EventTypes;
            return types.Count == 0 || types.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }
    }
}
