using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Events;

public record DepartmentCreatedEvent(
    Guid DepartmentId,
    string Name,
    string Code) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record DepartmentUpdatedEvent(
    Guid DepartmentId,
    string Name,
    string Code) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PositionCreatedEvent(
    Guid PositionId,
    string Title,
    string Code) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record EmployeeHiredEvent(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    string? Email,
    Guid DepartmentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record EmployeeTerminatedEvent(
    Guid EmployeeId,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record EmployeeTransferredEvent(
    Guid EmployeeId,
    Guid OldDepartmentId,
    Guid NewDepartmentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record UnitOfMeasureCreatedEvent(
    Guid UnitOfMeasureId,
    string Code,
    string Name) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CountryCreatedEvent(
    Guid CountryId,
    string Code,
    string Name) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
