namespace ERP.SHP.Application.DTOs.Settings;

/// <summary>Platform switches: which carrier integrations exist at all. A shop still needs its own account at each carrier.</summary>
public record SystemShippingSettingsDto(bool CustomCarriersAllowed, bool GhtkAvailable, bool VtpAvailable, bool GhnAvailable, bool JntAvailable);
public record UpdateSystemShippingSettingsRequest(bool CustomCarriersAllowed, bool GhtkAvailable = true, bool VtpAvailable = true, bool GhnAvailable = true, bool JntAvailable = true);

/// <summary>The shop's shipping settings. The sender is where the carriers collect the parcels; an incomplete one makes every booking fail with a clear reason.</summary>
public record TenantShippingSettingsDto(
    Guid TenantId, decimal DefaultShippingFee, decimal FreeShippingThreshold,
    string? SenderName, string? SenderPhone, string? SenderAddress, string? SenderWard, string? SenderDistrict, string? SenderProvince,
    int DefaultItemWeightGrams);

public record UpdateTenantShippingSettingsRequest(
    decimal DefaultShippingFee, decimal FreeShippingThreshold,
    string? SenderName = null, string? SenderPhone = null, string? SenderAddress = null, string? SenderWard = null, string? SenderDistrict = null, string? SenderProvince = null,
    int DefaultItemWeightGrams = 500);

/// <summary>One carrier as the shop's admin sees it. Secrets are never returned, only whether they are set.</summary>
public record CarrierAccountDto(
    string Code, string Name, bool Integrated, bool PlatformAvailable, bool Enabled, bool Sandbox,
    string? PublicId, string? Username, bool HasSecret, bool HasWebhookSecret, bool Complete);

/// <summary>A null or empty secret keeps the stored one.</summary>
public record UpdateCarrierAccountRequest(bool Enabled, bool Sandbox, string? PublicId, string? Username, string? Secret, string? WebhookSecret);
