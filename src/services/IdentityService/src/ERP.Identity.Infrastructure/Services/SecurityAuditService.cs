using ERP.Identity.Application.DTOs.Security;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Infrastructure.Services
{
    public class SecurityAuditService : ISecurityAuditService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SecurityAuditService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task LogAsync(
            string eventType, bool isSuccess, Guid? tenantId = null, Guid? userId = null,
            string? email = null, string? ipAddress = null, string? userAgent = null,
            string? details = null, string? errorReason = null, CancellationToken ct = default)
        {
            var log = SecurityAuditLog.Create(eventType, isSuccess, tenantId, userId, email, ipAddress, userAgent, details, errorReason);
            await _unitOfWork.SecurityAuditLogs.AddAsync(log, ct);
        }

        public async Task RecordLoginAttemptAsync(
            string ipAddress, bool isSuccessful, string? email = null, Guid? userId = null,
            string? failureReason = null, CancellationToken ct = default)
        {
            var attempt = LoginAttempt.Create(ipAddress, isSuccessful, email, userId, failureReason);
            await _unitOfWork.LoginAttempts.AddAsync(attempt, ct);
        }

        public async Task<PagedResult<SecurityAuditLogDto>> SearchAuditLogsAsync(
            Guid? tenantId, Guid? userId, string? eventType, int page, int pageSize, CancellationToken ct)
        {
            var (items, total) = await _unitOfWork.SecurityAuditLogs.SearchAsync(tenantId, userId, eventType, page, pageSize, ct);
            var dtos = items.Select(l => new SecurityAuditLogDto(
                l.Id, l.TenantId, l.UserId, l.Email, l.EventType, l.IsSuccess,
                l.IpAddress, l.UserAgent, l.Details, l.ErrorReason, l.OccurredAt)).ToList();
            return new PagedResult<SecurityAuditLogDto>(dtos, total, page, pageSize);
        }

        public async Task<PagedResult<LoginAttemptDto>> SearchLoginAttemptsAsync(
            string? ipAddress, string? email, int page, int pageSize, CancellationToken ct)
        {
            var (items, total) = await _unitOfWork.LoginAttempts.SearchAsync(ipAddress, email, page, pageSize, ct);
            var dtos = items.Select(a => new LoginAttemptDto(
                a.Id, a.IpAddress, a.Email, a.UserId, a.IsSuccessful, a.FailureReason, a.AttemptedAt)).ToList();
            return new PagedResult<LoginAttemptDto>(dtos, total, page, pageSize);
        }
    }
}
