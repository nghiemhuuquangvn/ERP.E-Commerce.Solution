namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Enforces password complexity (min length + required character classes) from the
    /// caller's own TenantAuthSettings — not a single global default — plus a reuse check
    /// against the last N entries in PasswordHistories (PasswordPolicy:HistoryCount in
    /// appsettings, default 5 — no per-tenant equivalent exists for this one). Does NOT call
    /// SaveChangesAsync — callers control the transaction boundary.
    /// </summary>
    public interface IPasswordPolicyService
    {
        /// <summary>Throws BusinessRuleException if the new password fails policy (too short,
        /// missing a required character class, or reused). Complexity rules (MinPasswordLength/
        /// RequireUppercase/Lowercase/Digit/Symbol) come from the tenant's own
        /// TenantAuthSettings, not a global appsettings default — see
        /// ISettingsService.GetOrCreateTenantSettingsEntityAsync.</summary>
        Task ValidateNewPasswordAsync(Guid tenantId, Guid userId, string newPlaintextPassword, CancellationToken ct);

        /// <summary>Records the password hash being replaced (the OLD hash) into history, then prunes beyond the configured count.</summary>
        Task RecordPasswordHistoryAsync(Guid userId, string previousPasswordHash, CancellationToken ct);
    }
}
