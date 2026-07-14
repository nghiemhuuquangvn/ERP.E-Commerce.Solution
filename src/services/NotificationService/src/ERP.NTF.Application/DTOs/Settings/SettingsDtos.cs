namespace ERP.NTF.Application.DTOs.Settings;

public record SystemNotificationSettingsDto(bool EmailChannelAvailable, bool SmsChannelAvailable, bool PushChannelAvailable);
public record UpdateSystemNotificationSettingsRequest(bool EmailChannelAvailable, bool SmsChannelAvailable, bool PushChannelAvailable);

public record TenantNotificationSettingsDto(Guid TenantId, bool EmailEnabledByDefault, bool SmsEnabledByDefault, bool PushEnabledByDefault);
public record UpdateTenantNotificationSettingsRequest(bool EmailEnabledByDefault, bool SmsEnabledByDefault, bool PushEnabledByDefault);

public record SystemEmailSettingsDto(bool CustomSmtpAllowed);
public record UpdateSystemEmailSettingsRequest(bool CustomSmtpAllowed);

/// <summary>HasSmtpPassword reports whether a password is stored, never the password itself.</summary>
public record TenantEmailSettingsDto(Guid TenantId, string? SmtpHost, int SmtpPort, string? SmtpUsername, bool HasSmtpPassword, string? FromAddress, string? FromName);

/// <summary>SmtpPassword is plaintext from the admin form; null/empty means "leave the existing stored password unchanged".</summary>
public record UpdateTenantEmailSettingsRequest(string? SmtpHost, int SmtpPort, string? SmtpUsername, string? SmtpPassword, string? FromAddress, string? FromName);

public record SystemSmsSettingsDto(bool CustomSmsGatewayAllowed);
public record UpdateSystemSmsSettingsRequest(bool CustomSmsGatewayAllowed);

/// <summary>HasApiKey reports whether an API key is stored, never the key itself.</summary>
public record TenantSmsSettingsDto(Guid TenantId, string? ProviderName, bool HasApiKey, string? FromNumber);

/// <summary>ApiKey is plaintext from the admin form; null/empty means "leave the existing stored key unchanged".</summary>
public record UpdateTenantSmsSettingsRequest(string? ProviderName, string? ApiKey, string? FromNumber);
