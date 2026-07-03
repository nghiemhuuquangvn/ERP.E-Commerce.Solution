using System;
using System.Collections.Generic;

namespace ERP.Identity.Application.DTOs.Security
{
    public record SecurityAuditLogDto(
        Guid Id, Guid? TenantId, Guid? UserId, string? Email, string EventType,
        bool IsSuccess, string? IpAddress, string? UserAgent, string? Details, string? ErrorReason, DateTime OccurredAt);

    public record LoginAttemptDto(
        Guid Id, string IpAddress, string? Email, Guid? UserId, bool IsSuccessful, string? FailureReason, DateTime AttemptedAt);

    public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

    public record DeviceSessionDto(
        Guid Id, string DeviceId, string? DeviceName, string? DeviceType, string? Platform,
        string? IpAddress, bool IsActive, DateTime LastActiveAt, DateTime CreatedAt);

    public record ApiKeyDto(
        Guid Id, Guid TenantId, string Name, string KeyPrefix, string? Scopes,
        bool IsActive, DateTime? ExpiresAt, DateTime? LastUsedAt, DateTime CreatedAt);

    /// <summary>Only ever returned once, at creation — RawKey is never persisted or recoverable afterwards.</summary>
    public record ApiKeyCreatedDto(ApiKeyDto Key, string RawKey);

    public record CreateApiKeyRequest(string Name, string? Scopes, int? ExpiresInDays);
}
