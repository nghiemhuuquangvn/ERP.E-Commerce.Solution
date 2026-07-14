using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities.Settings;

/// <summary>
/// Per-tenant SMS gateway configuration. One row per TenantId (unique).
/// <see cref="ApiKeyEncrypted"/> is stored already-encrypted (ISecretProtector, reused
/// from Phase 1). Not yet wired into TwilioSmsSender, which still reads Twilio:* from
/// appsettings.json.
/// </summary>
public class TenantSmsSettings : BaseEntity
{
    public string? ProviderName { get; private set; }
    public string? ApiKeyEncrypted { get; private set; }
    public string? FromNumber { get; private set; }

    protected TenantSmsSettings() { }

    public static TenantSmsSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantSmsSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>apiKeyEncrypted must already be encrypted by the caller — null/unchanged keeps the existing stored value.</summary>
    public void Update(string? providerName, string? apiKeyEncrypted, string? fromNumber)
    {
        ProviderName = providerName;
        if (apiKeyEncrypted != null)
            ApiKeyEncrypted = apiKeyEncrypted;
        FromNumber = fromNumber;
    }
}
