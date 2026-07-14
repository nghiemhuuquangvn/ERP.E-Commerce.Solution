namespace ERP.PRD.Application.DTOs.Settings;

public record SystemStoreProductSettingsDto(bool PriceDisplayIncludesTaxByDefault, bool ProductReviewsEnabled);
public record UpdateSystemStoreProductSettingsRequest(bool PriceDisplayIncludesTaxByDefault, bool ProductReviewsEnabled);

public record TenantStoreProductSettingsDto(Guid TenantId, string StoreName, string? StoreDescription, string? LogoUrl, bool ShowOutOfStockProducts);
public record UpdateTenantStoreProductSettingsRequest(string StoreName, string? StoreDescription, string? LogoUrl, bool ShowOutOfStockProducts);
