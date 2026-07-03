using ERP.Identity.Application.DTOs.Security;

namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Writes to SecurityAuditLogs/LoginAttempts (previously orphaned SQL tables with
    /// no C# entity at all) and reads them back for the audit-log query endpoints.
    /// Does NOT call SaveChangesAsync — callers control the transaction boundary,
    /// same convention as IUnitOfWork.* repositories.
    /// </summary>
    public interface ISecurityAuditService
    {
        Task LogAsync(
            string eventType, bool isSuccess, Guid? tenantId = null, Guid? userId = null,
            string? email = null, string? ipAddress = null, string? userAgent = null,
            string? details = null, string? errorReason = null, CancellationToken ct = default);

        Task RecordLoginAttemptAsync(
            string ipAddress, bool isSuccessful, string? email = null, Guid? userId = null,
            string? failureReason = null, CancellationToken ct = default);

        Task<PagedResult<SecurityAuditLogDto>> SearchAuditLogsAsync(
            Guid? tenantId, Guid? userId, string? eventType, int page, int pageSize, CancellationToken ct);

        Task<PagedResult<LoginAttemptDto>> SearchLoginAttemptsAsync(
            string? ipAddress, string? email, int page, int pageSize, CancellationToken ct);
    }
}
