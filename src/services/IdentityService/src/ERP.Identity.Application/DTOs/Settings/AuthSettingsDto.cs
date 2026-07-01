using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemAuthSettingsDto(bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled);
    public record UpdateSystemAuthSettingsRequest(bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled);

    public record TenantAuthSettingsDto(Guid TenantId, bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled, bool TwoFactorRequired);
    public record UpdateTenantAuthSettingsRequest(bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled, bool TwoFactorRequired);

    public record PublicAuthOptionsDto(bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled);
}
