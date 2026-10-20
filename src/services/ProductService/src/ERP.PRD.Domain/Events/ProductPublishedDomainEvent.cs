using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    public class ProductPublishedDomainEvent : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid ProductId { get; }
        public string Name { get; }
        public DateTime OccurredOn { get; }

        public ProductPublishedDomainEvent(Guid productId, string name)
        {
            ProductId = productId;
            Name = name;
            OccurredOn = DateTime.UtcNow;
        }
    }
}
