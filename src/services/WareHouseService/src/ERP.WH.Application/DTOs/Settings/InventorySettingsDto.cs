namespace ERP.WH.Application.DTOs.Settings;

public record SystemInventorySettingsDto(bool NegativeStockAllowed);
public record UpdateSystemInventorySettingsRequest(bool NegativeStockAllowed);

public record TenantInventorySettingsDto(Guid TenantId, int LowStockThreshold, bool BackorderAllowed, int DefaultHoldDays);
public record UpdateTenantInventorySettingsRequest(int LowStockThreshold, bool BackorderAllowed, int DefaultHoldDays);
