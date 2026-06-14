using System.ComponentModel.DataAnnotations;

namespace ERP.Shared.Core.Domain
{
    /// <summary>
    /// Base class for all domain entities.
    /// Provides: tenant isolation (TenantId), soft-delete (IsDeleted),
    /// audit fields (CreatedBy/At, UpdatedBy/At), optimistic locking (RowVersion),
    /// and domain event collection.
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Tenant ID for multi-tenant data isolation.
        /// All entities must belong to a tenant and should be filtered by tenant.
        /// </summary>
        public Guid TenantId { get; set; }

        public string? Description { get; set; }
        public bool IsDeleted { get; protected set; } = false;

        // Audit Trail (set by application layer from user context)
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Row version for optimistic locking (SQL Server ROWVERSION).
        /// Automatically managed by EF Core when using [Timestamp] attribute in config.
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
