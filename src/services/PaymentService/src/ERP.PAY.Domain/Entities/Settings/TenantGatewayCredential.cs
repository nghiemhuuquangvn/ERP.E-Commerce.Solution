using ERP.Shared.Core.Domain;

namespace ERP.PAY.Domain.Entities.Settings;

/// <summary>
/// One tenant's merchant account at one payment gateway (VNPay, MoMo, ZaloPay). Secrets are stored already encrypted
/// (ISecretProtector) — this entity never sees plaintext. Meaning of the fields per provider:
/// VNPay: PublicId = TmnCode, Secret1 = hash secret. MoMo: PublicId = partnerCode, AccessKey, Secret1 = secret key.
/// ZaloPay: PublicId = app_id, Secret1 = key1 (creating orders), Secret2 = key2 (verifying callbacks).
/// </summary>
public class TenantGatewayCredential : BaseEntity
{
    public string Provider { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public bool Sandbox { get; private set; } = true;
    public string? PublicId { get; private set; }
    /// <summary>MoMo's access key; stored encrypted like the secrets.</summary>
    public string? AccessKey { get; private set; }
    public string? Secret1Encrypted { get; private set; }
    public string? Secret2Encrypted { get; private set; }

    protected TenantGatewayCredential() { }

    public static TenantGatewayCredential Create(Guid tenantId, string provider)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Provider is required", nameof(provider));

        return new TenantGatewayCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>A null secret keeps the stored one (the admin form never shows secrets back); an empty string is treated as "unchanged" too.</summary>
    public void Update(bool enabled, bool sandbox, string? publicId, string? accessKey, string? secret1Encrypted, string? secret2Encrypted)
    {
        Enabled = enabled;
        Sandbox = sandbox;
        if (publicId != null) PublicId = publicId.Trim();
        if (!string.IsNullOrEmpty(accessKey)) AccessKey = accessKey;
        if (!string.IsNullOrEmpty(secret1Encrypted)) Secret1Encrypted = secret1Encrypted;
        if (!string.IsNullOrEmpty(secret2Encrypted)) Secret2Encrypted = secret2Encrypted;
        UpdatedAt = DateTime.UtcNow;
    }
}
