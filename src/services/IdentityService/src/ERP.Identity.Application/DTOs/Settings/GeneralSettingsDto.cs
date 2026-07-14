using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemGeneralSettingsDto(bool MaintenanceModeEnabled, string DefaultLanguageCode);
    public record UpdateSystemGeneralSettingsRequest(bool MaintenanceModeEnabled, string DefaultLanguageCode);

    public record TenantGeneralSettingsDto(Guid TenantId, string SiteName, string DefaultLanguageCode, string TimeZone, string DateFormat, bool MaintenanceModeEnabled);
    public record UpdateTenantGeneralSettingsRequest(string SiteName, string DefaultLanguageCode, string TimeZone, string DateFormat, bool MaintenanceModeEnabled);
}
