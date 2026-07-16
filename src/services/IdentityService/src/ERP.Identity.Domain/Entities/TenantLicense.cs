using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// The issued license record for a tenant — one current license per tenant (unique on
    /// TenantId). Admin-issued/DB-driven, no self-serve billing. Expiry is NOT a stored
    /// status — it's computed live via <see cref="IsCurrentlyValid"/> from Status +
    /// ExpiresAt + StartsAt vs. UtcNow, so a suspended/revoked tenant can be reinstated
    /// without losing the original ExpiresAt, and an expired-but-not-revoked license still
    /// reports its true Status for audit/history purposes. Standalone POCO (not BaseEntity).
    /// </summary>
    public class TenantLicense
    {
        public Guid Id { get; private set; }
        public Guid TenantId { get; private set; }
        public Guid? PlanId { get; private set; }
        public int? MaxUsers { get; private set; }

        /// <summary>Display-only, e.g. "ERP-XXXX-XXXX-XXXX". NOT the enforcement mechanism —
        /// Status + ExpiresAt is the source of truth.</summary>
        public string LicenseKey { get; private set; } = default!;

        public DateTime IssuedAt { get; private set; }
        public DateTime StartsAt { get; private set; }
        public DateTime? ExpiresAt { get; private set; }

        /// <summary>"Active" | "Suspended" | "Revoked".</summary>
        public string Status { get; private set; } = default!;

        public string IssuedBy { get; private set; } = default!;
        public string? Notes { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private TenantLicense() { }

        public static string GenerateLicenseKey() =>
            $"ERP-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        public static TenantLicense Create(
            Guid tenantId, Guid? planId, int? maxUsers, DateTime startsAt, DateTime? expiresAt,
            string issuedBy, string? notes)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(issuedBy))
                throw new ArgumentException("IssuedBy is required", nameof(issuedBy));

            var now = DateTime.UtcNow;
            return new TenantLicense
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanId = planId,
                MaxUsers = maxUsers,
                LicenseKey = GenerateLicenseKey(),
                IssuedAt = now,
                StartsAt = startsAt,
                ExpiresAt = expiresAt,
                Status = "Active",
                IssuedBy = issuedBy,
                Notes = notes,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        /// <summary>
        /// Re-issues an existing tenant license row in place (the unique TenantId index
        /// means a tenant can only ever have one TenantLicense row — a fresh "Issue" after
        /// a prior Revoke reuses the same row rather than violating the unique constraint).
        /// Generates a new LicenseKey and resets Status to Active.
        /// </summary>
        public void Reissue(Guid? planId, int? maxUsers, DateTime startsAt, DateTime? expiresAt, string issuedBy, string? notes)
        {
            if (string.IsNullOrWhiteSpace(issuedBy))
                throw new ArgumentException("IssuedBy is required", nameof(issuedBy));

            PlanId = planId;
            MaxUsers = maxUsers;
            LicenseKey = GenerateLicenseKey();
            IssuedAt = DateTime.UtcNow;
            StartsAt = startsAt;
            ExpiresAt = expiresAt;
            Status = "Active";
            IssuedBy = issuedBy;
            Notes = notes;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Renew(DateTime? expiresAt, int? maxUsers, string? notes)
        {
            ExpiresAt = expiresAt;
            if (maxUsers.HasValue)
                MaxUsers = maxUsers;
            if (notes != null)
                Notes = notes;
            Status = "Active";
            UpdatedAt = DateTime.UtcNow;
        }

        public void Suspend()
        {
            Status = "Suspended";
            UpdatedAt = DateTime.UtcNow;
        }

        public void Revoke()
        {
            Status = "Revoked";
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsCurrentlyValid =>
            Status == "Active" && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow) && StartsAt <= DateTime.UtcNow;
    }
}
