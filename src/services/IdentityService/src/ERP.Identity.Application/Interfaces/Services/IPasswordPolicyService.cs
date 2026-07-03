namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Enforces the "Password policy configuration" gap: a configurable minimum
    /// length (PasswordPolicy:MinLength in appsettings) plus a reuse check against
    /// the last N entries in PasswordHistories (PasswordPolicy:HistoryCount,
    /// default 5) — the table's own SQL comment says "prevent reuse of last 10
    /// passwords", this makes the count configurable rather than hardcoding 10.
    /// Does NOT call SaveChangesAsync — callers control the transaction boundary.
    /// </summary>
    public interface IPasswordPolicyService
    {
        /// <summary>Throws BusinessRuleException if the new password fails policy (too short or reused).</summary>
        Task ValidateNewPasswordAsync(Guid userId, string newPlaintextPassword, CancellationToken ct);

        /// <summary>Records the password hash being replaced (the OLD hash) into history, then prunes beyond the configured count.</summary>
        Task RecordPasswordHistoryAsync(Guid userId, string previousPasswordHash, CancellationToken ct);
    }
}
