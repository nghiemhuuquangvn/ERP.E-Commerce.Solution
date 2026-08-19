namespace ERP.Identity.Infrastructure.Localization
{
    /// <summary>
    /// Marker type used as the generic type anchor for IStringLocalizer&lt;AuthMessages&gt;,
    /// backed by Resources/Localization/AuthMessages.resx (and its per-culture siblings,
    /// e.g. Resources/Localization/AuthMessages.vi-VN.resx) — IdentityService-specific
    /// messages (password policy, invite conflicts) that don't belong in ERP.Shared's
    /// generic SharedResource.
    /// </summary>
    public sealed class AuthMessages
    {
    }
}
