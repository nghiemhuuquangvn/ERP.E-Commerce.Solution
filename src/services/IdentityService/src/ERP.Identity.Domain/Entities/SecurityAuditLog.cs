using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Append-only security event record — maps to the SecurityAuditLogs table that
    /// has existed in 001_CreateAuthDB.sql since the schema was first written but had
    /// no corresponding C# entity until now. Deliberately NOT a BaseEntity: the SQL
    /// table has no soft-delete/UpdatedAt columns (rows are immutable once written,
    /// "no UPDATE/DELETE" per the table's own header comment).
    /// </summary>
    public class SecurityAuditLog
    {
        public Guid Id { get; private set; }
        public Guid? TenantId { get; private set; }
        public Guid? UserId { get; private set; }
        public string? Email { get; private set; }
        public string EventType { get; private set; } = default!;
        public bool IsSuccess { get; private set; }
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public string? Details { get; private set; }
        public string? ErrorReason { get; private set; }
        public DateTime OccurredAt { get; private set; }

        private SecurityAuditLog() { }

        public static SecurityAuditLog Create(
            string eventType,
            bool isSuccess,
            Guid? tenantId = null,
            Guid? userId = null,
            string? email = null,
            string? ipAddress = null,
            string? userAgent = null,
            string? details = null,
            string? errorReason = null)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("EventType is required", nameof(eventType));

            return new SecurityAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Email = email,
                EventType = eventType,
                IsSuccess = isSuccess,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = details,
                ErrorReason = errorReason,
                OccurredAt = DateTime.UtcNow
            };
        }
    }
}
