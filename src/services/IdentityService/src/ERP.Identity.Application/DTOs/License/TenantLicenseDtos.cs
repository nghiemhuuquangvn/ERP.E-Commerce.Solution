using System;

namespace ERP.Identity.Application.DTOs.License
{
    public record TenantLicenseDto(
        Guid Id,
        Guid TenantId,
        Guid? PlanId,
        int? MaxUsers,
        string LicenseKey,
        DateTime IssuedAt,
        DateTime StartsAt,
        DateTime? ExpiresAt,
        string Status,
        bool IsCurrentlyValid,
        string IssuedBy,
        string? Notes,
        string[] ModuleCodes);

    /// <summary>
    /// ModuleCodes, when provided, overrides the chosen plan's default module set.
    /// StartsAt defaults to now (UTC) when null; ExpiresAt null = perpetual.
    /// </summary>
    public record IssueLicenseRequest(
        Guid? PlanId,
        string[]? ModuleCodes,
        int? MaxUsers,
        DateTime? StartsAt,
        DateTime? ExpiresAt,
        string? Notes);

    /// <summary>
    /// Renewal reactivates a Suspended/Revoked license back to Active. ModuleCodes/MaxUsers/
    /// Notes, when omitted, leave the current values unchanged; ExpiresAt is always applied
    /// (null = perpetual).
    /// </summary>
    public record RenewLicenseRequest(
        DateTime? ExpiresAt,
        int? MaxUsers,
        string[]? ModuleCodes,
        string? Notes);
}
