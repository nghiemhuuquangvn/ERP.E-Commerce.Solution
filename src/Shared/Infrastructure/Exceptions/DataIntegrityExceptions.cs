namespace ERP.Shared.Infrastructure.Exceptions;

/// <summary>
/// Thrown when a concurrency conflict occurs (optimistic locking violation).
/// User should refresh the data and try again.
/// </summary>
public class ConcurrencyException : Exception
{
    public Guid? EntityId { get; }

    public ConcurrencyException(string message, Guid? entityId = null)
        : base(message)
    {
        EntityId = entityId;
    }

    public ConcurrencyException(string message, Exception innerException, Guid? entityId = null)
        : base(message, innerException)
    {
        EntityId = entityId;
    }
}

/// <summary>
/// Thrown when a tenant violation is detected.
/// A user from Tenant A tried to access data from Tenant B.
/// </summary>
public class TenantIsolationException : Exception
{
    public Guid EntityId { get; }
    public Guid RequestingTenantId { get; }
    public Guid UnauthorizedTenantId { get; }

    public TenantIsolationException(Guid entityId, Guid requestingTenantId, Guid unauthorizedTenantId)
        : base($"Tenant isolation violation: Entity {entityId} belongs to tenant {unauthorizedTenantId}, " +
               $"but access was requested by tenant {requestingTenantId}")
    {
        EntityId = entityId;
        RequestingTenantId = requestingTenantId;
        UnauthorizedTenantId = unauthorizedTenantId;
    }
}

/// <summary>
/// Thrown when business rule validation fails.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class BusinessRuleException : Exception
{
    public string RuleName { get; }

    public BusinessRuleException(string ruleName, string message)
        : base(message)
    {
        RuleName = ruleName;
    }
}

/// <summary>
/// Thrown when a referenced entity is not found.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class EntityNotFoundException : Exception
{
    public string EntityName { get; }
    public Guid EntityId { get; }

    public EntityNotFoundException(string entityName, Guid entityId)
        : base($"{entityName} with ID {entityId} was not found")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}

/// <summary>
/// Thrown when a unique constraint is violated.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class DuplicateException : Exception
{
    public string PropertyName { get; }
    public object PropertyValue { get; }

    public DuplicateException(string propertyName, object propertyValue)
        : base($"{propertyName} '{propertyValue}' already exists")
    {
        PropertyName = propertyName;
        PropertyValue = propertyValue;
    }
}
