namespace ERP.PO.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
