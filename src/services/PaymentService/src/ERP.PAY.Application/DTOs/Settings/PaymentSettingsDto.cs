namespace ERP.PAY.Application.DTOs.Settings;

public record SystemPaymentSettingsDto(bool VnPayAvailable, bool ManualBankTransferAvailable, bool MoMoAvailable = true, bool ZaloPayAvailable = true);
public record UpdateSystemPaymentSettingsRequest(bool VnPayAvailable, bool ManualBankTransferAvailable, bool MoMoAvailable = true, bool ZaloPayAvailable = true);

/// <summary>HasVnPaySecretKey reports whether a key is stored, never the key itself.</summary>
public record TenantPaymentSettingsDto(Guid TenantId, bool VnPayEnabled, bool HasVnPaySecretKey, bool SandboxMode, bool CodEnabled = true);

/// <summary>VnPaySecretKey is plaintext from the admin form; null/empty means "leave the existing stored key unchanged". Encrypted server-side before persisting.</summary>
public record UpdateTenantPaymentSettingsRequest(bool VnPayEnabled, string? VnPaySecretKey, bool SandboxMode, bool CodEnabled = true);

/// <summary>One gateway account of a tenant. Secrets are never returned, only whether they are set.</summary>
public record GatewayCredentialDto(string Provider, bool Enabled, bool Sandbox, string? PublicId, bool HasAccessKey, bool HasSecret1, bool HasSecret2);

/// <summary>Plaintext from the admin form; null / empty secrets and access key leave the stored value unchanged. Encrypted server-side before persisting.</summary>
public record UpdateGatewayCredentialRequest(bool Enabled, bool Sandbox, string? PublicId, string? AccessKey, string? Secret1, string? Secret2);
