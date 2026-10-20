using ERP.Shared.Core.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.Shared.Messaging
{
    /// <summary>
    /// Carries the tenant across service boundaries in the event payload (decision
    /// 2026-10-20): publishers stamp <see cref="ITenantEvent.TenantId"/> when staging the
    /// event in the Outbox, consumers read it back and handle the message inside
    /// <c>TenantScope.Begin(tenantId)</c>.
    /// </summary>
    public static class EventTenant
    {
        /// <summary>
        /// Sets the event's TenantId to the tenant of the entity that raised it, unless the
        /// event already carries one. Call from every Outbox writer before serializing.
        /// </summary>
        public static void Stamp(object domainEvent, Guid tenantId)
        {
            if (domainEvent is ITenantEvent tenantEvent
                && tenantEvent.TenantId == Guid.Empty
                && tenantId != Guid.Empty)
                tenantEvent.TenantId = tenantId;
        }

        /// <summary>Stamps every pending domain event of <paramref name="entity"/> with its tenant.</summary>
        public static void StampFrom(BaseEntity entity)
        {
            foreach (var domainEvent in entity.DomainEvents)
                Stamp(domainEvent, entity.TenantId);
        }

        /// <summary>
        /// Reads the top-level <c>TenantId</c> (any casing) of a serialized event. False when
        /// the payload has none, it is empty, or the JSON is unreadable — the consumer must
        /// then not guess (skip, or derive the tenant from data the event references).
        /// </summary>
        public static bool TryRead(string? json, out Guid tenantId)
        {
            tenantId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                if (JToken.Parse(json) is not JObject obj)
                    return false;

                var token = obj.GetValue("TenantId", StringComparison.OrdinalIgnoreCase);
                return token is not null
                    && Guid.TryParse(token.Type == JTokenType.String ? token.Value<string>() : token.ToString(), out tenantId)
                    && tenantId != Guid.Empty;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>Tenant of an already-deserialized event, if it carries one.</summary>
        public static bool TryGet(object? domainEvent, out Guid tenantId)
        {
            tenantId = domainEvent is ITenantEvent { TenantId: var id } ? id : Guid.Empty;
            return tenantId != Guid.Empty;
        }
    }
}
