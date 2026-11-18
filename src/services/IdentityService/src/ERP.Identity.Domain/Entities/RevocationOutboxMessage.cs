using System;

namespace ERP.Identity.Domain.Entities
{
    public enum RevocationKind : byte
    {
        /// <summary>One access token (logout): <see cref="RevocationOutboxMessage.Subject"/> is its jti.</summary>
        Token = 1,
        /// <summary>Every token a user holds (password, lock, role change): Subject is the user id.</summary>
        Users = 2,
        /// <summary>Every token of a tenant (licence, tenant status): Subject is the tenant id.</summary>
        Tenant = 3,
    }

    /// <summary>
    /// One token revocation waiting to reach the shared revocation store (security review 2026-11, S4a, SEC-M8;
    /// docs/business/Identity/TokenRevocation.md). Written in the SAME commit as the change that caused it, so a
    /// committed lock / password / role change can never lose its revocation; published right after the commit and,
    /// when the store is down, again by the RevocationOutboxPublisher until it succeeds - never dropped.
    /// Not tenant-owned: AuthDbContext has explicit predicates, and the publisher works across tenants.
    /// </summary>
    public class RevocationOutboxMessage
    {
        public Guid Id { get; private set; }
        public RevocationKind Kind { get; private set; }
        public string Subject { get; private set; } = default!;
        /// <summary>Token only: when the access token expires (the revocation is pointless after that).</summary>
        public DateTime? ExpiresAt { get; private set; }
        /// <summary>When the change happened (unix seconds) - the marker carries this, not the publish time.</summary>
        public long ChangedAtUnix { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? PublishedAt { get; private set; }
        public int Attempts { get; private set; }
        public string? LastError { get; private set; }

        private RevocationOutboxMessage() { }

        private static RevocationOutboxMessage Create(RevocationKind kind, string subject, DateTime? expiresAt, DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Subject = subject,
            ExpiresAt = expiresAt,
            ChangedAtUnix = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            CreatedAt = now,
        };

        public static RevocationOutboxMessage ForToken(string jti, DateTime expiresAtUtc, DateTime now) =>
            Create(RevocationKind.Token, jti, expiresAtUtc, now);

        public static RevocationOutboxMessage ForUser(Guid userId, DateTime now) =>
            Create(RevocationKind.Users, userId.ToString(), null, now);

        public static RevocationOutboxMessage ForTenant(Guid tenantId, DateTime now) =>
            Create(RevocationKind.Tenant, tenantId.ToString(), null, now);

        public bool IsPublished => PublishedAt != null;

        public void MarkPublished(DateTime now)
        {
            PublishedAt = now;
            LastError = null;
        }

        public void MarkFailed(string error)
        {
            Attempts++;
            LastError = error.Length > 1000 ? error[..1000] : error;
        }
    }
}
