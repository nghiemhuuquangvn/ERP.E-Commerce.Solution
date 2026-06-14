using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    public class ProductCreatedDomainEvent : IDomainEvent
    {
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
