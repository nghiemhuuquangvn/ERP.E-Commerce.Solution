using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemLocalizationSettingsDto(bool MultiLanguageAllowed, string DefaultLocale);
    public record UpdateSystemLocalizationSettingsRequest(bool MultiLanguageAllowed, string DefaultLocale);

    public record TenantLocalizationSettingsDto(Guid TenantId, string DefaultLocale, string SupportedLocales, string NumberFormatLocale, bool RtlEnabled);
    public record UpdateTenantLocalizationSettingsRequest(string DefaultLocale, string SupportedLocales, string NumberFormatLocale, bool RtlEnabled);
}
