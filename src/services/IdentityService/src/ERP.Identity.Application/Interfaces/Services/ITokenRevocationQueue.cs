namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Collects the token revocations caused by the current unit of work and publishes them
    /// to the platform-wide revocation store (ERP.Shared.Authorization.TokenRevocation) only
    /// AFTER the database commit succeeded — so a rolled-back change never logs anyone out,
    /// and every service rejects the affected tokens right after a successful one.
    /// Reached through <see cref="Repositories.IUnitOfWork.Revocations"/>, which flushes it.
    /// <para>
    /// Rule (CLAUDE.md "Identity invariants"): every change that affects what a token may do —
    /// logout, password change/reset, lock, deactivation, role grants, role permission edits,
    /// tenant status, license — must call one of these.
    /// </para>
    /// </summary>
    public interface ITokenRevocationQueue
    {
        /// <summary>Revoke one access token (logout).</summary>
        void RevokeToken(string jti, DateTime expiresAtUtc);

        /// <summary>Every token these users hold now becomes invalid (they refresh or sign in again).</summary>
        void UsersChanged(params Guid[] userIds);

        /// <summary>Every token of the tenant becomes invalid (license / tenant status).</summary>
        void TenantChanged(Guid tenantId);

        /// <summary>Publishes and clears the pending revocations. Never throws: a store failure is
        /// logged — the database change already happened.</summary>
        Task FlushAsync(CancellationToken ct);

        /// <summary>Drops pending revocations (rollback).</summary>
        void Clear();
    }
}
