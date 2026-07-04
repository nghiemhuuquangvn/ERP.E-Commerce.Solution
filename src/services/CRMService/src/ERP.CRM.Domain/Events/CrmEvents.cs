using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Events;

public record LeadCreatedEvent(Guid LeadId, string LeadName) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LeadQualifiedEvent(Guid LeadId, Guid OpportunityId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LeadLostEvent(Guid LeadId, string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record OpportunityCreatedEvent(Guid OpportunityId, Guid ContactId, string Title, decimal Value) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record OpportunityWonEvent(Guid OpportunityId, decimal Value) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record OpportunityLostEvent(Guid OpportunityId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CaseOpenedEvent(Guid CaseId, Guid ContactId, string Title) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CaseResolvedEvent(Guid CaseId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ContactCreatedEvent(Guid ContactId, string Name, string? Email) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ActivityCreatedEvent(
    Guid ActivityId, Guid ContactId, string ActivityType, string Subject) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ActivityCompletedEvent(Guid ActivityId, Guid ContactId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
