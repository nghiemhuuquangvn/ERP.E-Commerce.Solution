namespace ERP.PAY.Application.DTOs.Settings;

public record SystemPaymentSettingsDto(bool VnPayAvailable, bool ManualBankTransferAvailable);
public record UpdateSystemPaymentSettingsRequest(bool VnPayAvailable, bool ManualBankTransferAvailable);

/// <summary>HasVnPaySecretKey reports whether a key is stored, never the key itself.</summary>
public record TenantPaymentSettingsDto(Guid TenantId, bool VnPayEnabled, bool HasVnPaySecretKey, bool SandboxMode);

/// <summary>VnPaySecretKey is plaintext from the admin form; null/empty means "leave the existing stored key unchanged". Encrypted server-side before persisting.</summary>
public record UpdateTenantPaymentSettingsRequest(bool VnPayEnabled, string? VnPaySecretKey, bool SandboxMode);
