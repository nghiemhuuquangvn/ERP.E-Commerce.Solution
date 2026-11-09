namespace ERP.WH.Application.DTOs.Settings;

public record SystemInventorySettingsDto(bool NegativeStockAllowed);
public record UpdateSystemInventorySettingsRequest(bool NegativeStockAllowed);

public record TenantInventorySettingsDto(Guid TenantId, decimal LowStockThreshold, bool BackorderAllowed, int DefaultHoldDays, Guid? DefaultWarehouseId = null);
/// <summary>DefaultWarehouseId = where stock without a warehouse goes; empty = the MAIN / first active warehouse.</summary>
public record UpdateTenantInventorySettingsRequest(decimal LowStockThreshold, bool BackorderAllowed, int DefaultHoldDays, Guid? DefaultWarehouseId = null);
