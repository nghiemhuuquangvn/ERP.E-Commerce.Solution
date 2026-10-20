using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    public class ProductCreatedDomainEvent : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid ProductId { get; }
        public string Name { get; }
        public string Sku { get; }
        public DateTime OccurredOn { get; }

        public ProductCreatedDomainEvent(Guid productId, string name, string sku)
        {
            ProductId = productId;
            Name = name;
            Sku = sku;
            OccurredOn = DateTime.UtcNow;
        }
    }
}
