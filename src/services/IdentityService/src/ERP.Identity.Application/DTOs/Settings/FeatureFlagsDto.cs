namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemFeatureFlagDto(string Key, bool IsEnabled, string? Description);
    public record UpsertSystemFeatureFlagRequest(bool IsEnabled, string? Description);

    /// <summary>IsOverridden reports whether the tenant has its own override row for this key, vs. inheriting the System value.</summary>
    public record TenantFeatureFlagDto(string Key, bool IsEnabled, bool IsOverridden, string? Description);
    public record SetTenantFeatureFlagOverrideRequest(bool IsEnabled);
}
