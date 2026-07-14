namespace ERP.SO.Application.DTOs.Settings;

public record SystemCartCheckoutSettingsDto(bool GuestCheckoutAllowed);
public record UpdateSystemCartCheckoutSettingsRequest(bool GuestCheckoutAllowed);

public record TenantCartCheckoutSettingsDto(Guid TenantId, int CartHoldMinutes, bool RequireRegistrationAtCheckout, int CheckoutSteps);
public record UpdateTenantCartCheckoutSettingsRequest(int CartHoldMinutes, bool RequireRegistrationAtCheckout, int CheckoutSteps);

public record SystemOrderSettingsDto(bool CustomOrderNumberFormatAllowed);
public record UpdateSystemOrderSettingsRequest(bool CustomOrderNumberFormatAllowed);

public record TenantOrderSettingsDto(Guid TenantId, string OrderNumberPrefix, int AutoCancelUnpaidAfterHours, bool AllowEditAfterSubmit);
public record UpdateTenantOrderSettingsRequest(string OrderNumberPrefix, int AutoCancelUnpaidAfterHours, bool AllowEditAfterSubmit);
