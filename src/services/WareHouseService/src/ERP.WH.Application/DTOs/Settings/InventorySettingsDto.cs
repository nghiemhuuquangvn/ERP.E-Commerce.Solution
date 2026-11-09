namespace ERP.WH.Application.DTOs.Settings;

public record SystemInventorySettingsDto(bool NegativeStockAllowed);
public record UpdateSystemInventorySettingsRequest(bool NegativeStockAllowed);

public record TenantInventorySettingsDto(Guid TenantId, decimal LowStockThreshold, bool BackorderAllowed, int DefaultHoldDays);
public record UpdateTenantInventorySettingsRequest(decimal LowStockThreshold, bool BackorderAllowed, int DefaultHoldDays);
