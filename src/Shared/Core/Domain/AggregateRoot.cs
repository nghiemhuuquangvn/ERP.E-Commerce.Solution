namespace ERP.Shared.Core.Domain
{
    /// <summary>
    /// Base class for aggregate roots.
    /// An aggregate root is a domain entity that serves as the entry point to an aggregate cluster.
    /// It is responsible for enforcing the invariants of the aggregate.
    /// </summary>
    public abstract class AggregateRoot : BaseEntity
    {
        // Aggregate roots can override behavior if needed, or simply inherit from BaseEntity
    }
}
