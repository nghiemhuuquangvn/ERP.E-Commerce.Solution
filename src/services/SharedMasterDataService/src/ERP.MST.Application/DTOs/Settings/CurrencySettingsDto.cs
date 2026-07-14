namespace ERP.MST.Application.DTOs.Settings;

public record SystemCurrencySettingsDto(bool MultiCurrencyAllowed);
public record UpdateSystemCurrencySettingsRequest(bool MultiCurrencyAllowed);

public record TenantCurrencySettingsDto(Guid TenantId, string DefaultCurrencyCode, string ExchangeRateSource, int DecimalDigits);
public record UpdateTenantCurrencySettingsRequest(string DefaultCurrencyCode, string ExchangeRateSource, int DecimalDigits);
