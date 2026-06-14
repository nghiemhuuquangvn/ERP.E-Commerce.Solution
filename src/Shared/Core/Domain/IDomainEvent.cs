namespace ERP.Shared.Core.Domain
{
    /// <summary>
    /// Marker interface for domain events.
    /// Domain events represent something that happened within the domain.
    /// They are collected by entities and published during SaveChangesAsync via the Outbox pattern.
    /// </summary>
    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
    }
}
