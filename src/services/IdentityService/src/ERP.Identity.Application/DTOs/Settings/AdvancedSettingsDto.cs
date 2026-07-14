using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemAdvancedSettingsDto(bool DebugModeAllowed);
    public record UpdateSystemAdvancedSettingsRequest(bool DebugModeAllowed);

    public record TenantAdvancedSettingsDto(Guid TenantId, bool DebugModeEnabled, string? CustomCssUrl, bool DataExportEnabled);
    public record UpdateTenantAdvancedSettingsRequest(bool DebugModeEnabled, string? CustomCssUrl, bool DataExportEnabled);
}
