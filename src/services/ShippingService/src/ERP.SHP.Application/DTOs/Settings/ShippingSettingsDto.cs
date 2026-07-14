namespace ERP.SHP.Application.DTOs.Settings;

public record SystemShippingSettingsDto(bool CustomCarriersAllowed);
public record UpdateSystemShippingSettingsRequest(bool CustomCarriersAllowed);

public record TenantShippingSettingsDto(Guid TenantId, decimal DefaultShippingFee, decimal FreeShippingThreshold);
public record UpdateTenantShippingSettingsRequest(decimal DefaultShippingFee, decimal FreeShippingThreshold);
