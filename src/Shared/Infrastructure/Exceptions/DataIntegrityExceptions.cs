namespace ERP.Shared.Infrastructure.Exceptions;

/// <summary>
/// Thrown when a concurrency conflict occurs (optimistic locking violation).
/// User should refresh the data and try again.
/// </summary>
public class ConcurrencyException : Exception
{
    public Guid? EntityId { get; }

    /// <summary>
    /// Set when the conflict is a caller error that retrying cannot fix (e.g. WH_IDEMPOTENCY_CONFLICT: the same key with another payload);
    /// a plain optimistic-locking conflict leaves it empty and may be retried. Answered as <c>errorCode</c> beside HTTP 409.
    /// </summary>
    public string? ErrorCode { get; }

    public ConcurrencyException(string message, Guid? entityId = null)
        : base(message)
    {
        EntityId = entityId;
    }

    public ConcurrencyException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
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

/// <summary>
/// A fault of the deployment, not of the request: a configuration value, key or wiring the service needs is missing
/// ("Authentication:Twitter:ClientId configuration is missing"). Maps to HTTP 500 with a generic message - the key name
/// goes to the log, never to the client (R101). It derives from <see cref="InvalidOperationException"/> so existing
/// catch blocks keep working, but the central refusal rule (own-code InvalidOperationException = 400) never applies to it.
/// </summary>
public class ServiceMisconfiguredException : InvalidOperationException
{
    public ServiceMisconfiguredException(string message) : base(message) { }
}
