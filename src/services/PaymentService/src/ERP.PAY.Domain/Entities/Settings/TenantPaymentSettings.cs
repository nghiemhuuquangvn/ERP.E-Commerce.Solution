using ERP.Shared.Core.Domain;

namespace ERP.PAY.Domain.Entities.Settings;

/// <summary>
/// Per-tenant Payment configuration. One row per TenantId (unique).
/// <see cref="VnPaySecretKeyEncrypted"/> is stored already-encrypted (via
/// ISecretProtector) — this entity never sees or stores the plaintext secret itself.
/// </summary>
public class TenantPaymentSettings : BaseEntity
{
    public bool VnPayEnabled { get; private set; } = false;
    public string? VnPaySecretKeyEncrypted { get; private set; }
    public bool SandboxMode { get; private set; } = true;
    /// <summary>Cash on delivery offered at checkout (no gateway; the money is collected when the goods are delivered).</summary>
    public bool CodEnabled { get; private set; } = true;

    protected TenantPaymentSettings() { }

    public static TenantPaymentSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantPaymentSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VnPayEnabled = false,
            SandboxMode = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>vnPaySecretKeyEncrypted must already be encrypted by the caller (ISecretProtector.Protect) — null/unchanged keeps the existing stored value.</summary>
    public void Update(bool vnPayEnabled, string? vnPaySecretKeyEncrypted, bool sandboxMode, bool codEnabled = true)
    {
        CodEnabled = codEnabled;
        VnPayEnabled = vnPayEnabled;
        if (vnPaySecretKeyEncrypted != null)
            VnPaySecretKeyEncrypted = vnPaySecretKeyEncrypted;
        SandboxMode = sandboxMode;
    }
}
