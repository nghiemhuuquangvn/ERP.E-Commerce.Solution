using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    public class ProductPublishedDomainEvent : IDomainEvent
    {
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
