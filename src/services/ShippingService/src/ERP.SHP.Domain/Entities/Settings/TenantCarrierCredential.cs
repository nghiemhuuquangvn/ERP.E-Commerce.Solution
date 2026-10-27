using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>
/// One tenant's account at one carrier. Secrets are stored already encrypted (ISecretProtector) — this entity never sees plaintext.
/// Meaning of the fields per carrier:
/// GHTK: PublicId = partner code (X-Client-Source), Secret1 = API token. GHN: PublicId = shop id, Secret1 = token.
/// Viettel Post: Username = login name, Secret1 = password. J&amp;T: PublicId = customer code, Secret1 = API key / private key.
/// </summary>
public class TenantCarrierCredential : BaseEntity
{
    public string CarrierCode { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public bool Sandbox { get; private set; } = true;
    public string? PublicId { get; private set; }
    public string? Username { get; private set; }
    public string? Secret1Encrypted { get; private set; }
    public string? Secret2Encrypted { get; private set; }

    protected TenantCarrierCredential() { }

    public static TenantCarrierCredential Create(Guid tenantId, string carrierCode)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required", nameof(tenantId));
        var code = CarrierCodes.Normalize(carrierCode) ?? throw new ArgumentException($"Unknown carrier '{carrierCode}'.", nameof(carrierCode));

        return new TenantCarrierCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CarrierCode = code,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>A null or empty secret keeps the stored one (the admin form never shows secrets back).</summary>
    public void Update(bool enabled, bool sandbox, string? publicId, string? username, string? secret1Encrypted, string? secret2Encrypted)
    {
        Enabled = enabled;
        Sandbox = sandbox;
        if (publicId != null) PublicId = publicId.Trim();
        if (username != null) Username = username.Trim();
        if (!string.IsNullOrEmpty(secret1Encrypted)) Secret1Encrypted = secret1Encrypted;
        if (!string.IsNullOrEmpty(secret2Encrypted)) Secret2Encrypted = secret2Encrypted;
        UpdatedAt = DateTime.UtcNow;
    }
}
