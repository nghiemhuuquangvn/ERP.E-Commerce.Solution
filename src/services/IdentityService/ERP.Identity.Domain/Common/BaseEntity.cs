using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Common
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Description { get; set; }
        public bool IsDeleted { get; protected set; } = false;
        public string? CreatedBy { get; protected set; }
        public DateTime? CreatedAt { get; protected set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; protected set; }
        public DateTime? UpdatedAt { get; protected set; } = DateTime.UtcNow;

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
