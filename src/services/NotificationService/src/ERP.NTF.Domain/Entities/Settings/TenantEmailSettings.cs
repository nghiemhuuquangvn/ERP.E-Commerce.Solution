using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities.Settings;

/// <summary>
/// Per-tenant SMTP configuration. One row per TenantId (unique).
/// <see cref="SmtpPasswordEncrypted"/> is stored already-encrypted (via
/// ERP.Shared.Security.ISecretProtector, reused from Phase 1's Payment Settings) —
/// this entity never sees or stores the plaintext password itself. Not yet wired
/// into SmtpEmailSender, which still reads Email:* from appsettings.json.
/// </summary>
public class TenantEmailSettings : BaseEntity
{
    public string? SmtpHost { get; private set; }
    public int SmtpPort { get; private set; } = 587;
    public string? SmtpUsername { get; private set; }
    public string? SmtpPasswordEncrypted { get; private set; }
    public string? FromAddress { get; private set; }
    public string? FromName { get; private set; }

    protected TenantEmailSettings() { }

    public static TenantEmailSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantEmailSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SmtpPort = 587,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>smtpPasswordEncrypted must already be encrypted by the caller — null/unchanged keeps the existing stored value.</summary>
    public void Update(string? smtpHost, int smtpPort, string? smtpUsername, string? smtpPasswordEncrypted, string? fromAddress, string? fromName)
    {
        SmtpHost = smtpHost;
        SmtpPort = smtpPort;
        SmtpUsername = smtpUsername;
        if (smtpPasswordEncrypted != null)
            SmtpPasswordEncrypted = smtpPasswordEncrypted;
        FromAddress = fromAddress;
        FromName = fromName;
    }
}
