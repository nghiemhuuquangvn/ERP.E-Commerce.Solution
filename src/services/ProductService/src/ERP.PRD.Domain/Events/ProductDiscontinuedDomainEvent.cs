using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    public class ProductDiscontinuedDomainEvent : IDomainEvent
    {
        public Guid ProductId { get; }
        public string Name { get; }
        public DateTime OccurredOn { get; }

        public ProductDiscontinuedDomainEvent(Guid productId, string name)
        {
            ProductId = productId;
            Name = name;
            OccurredOn = DateTime.UtcNow;
        }
    }
}
