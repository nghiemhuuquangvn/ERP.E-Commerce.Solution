using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemAuditSettingsDto(bool AuditLoggingEnabled);
    public record UpdateSystemAuditSettingsRequest(bool AuditLoggingEnabled);

    public record TenantAuditSettingsDto(Guid TenantId, int RetentionDays, bool LogSuccessfulLogins, bool LogFailedLogins, bool LogPermissionChanges);
    public record UpdateTenantAuditSettingsRequest(int RetentionDays, bool LogSuccessfulLogins, bool LogFailedLogins, bool LogPermissionChanges);
}
