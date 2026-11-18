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

        /// <summary>
        /// S4a (SEC-M8, docs/business/Identity/TokenRevocation.md): turns the pending revocations into outbox rows (stamped
        /// with the change time) and clears them. The unit of work saves the rows in the SAME commit as the change.
        /// </summary>
        IReadOnlyList<Domain.Entities.RevocationOutboxMessage> Stage();

        /// <summary>Publishes the rows to the shared store and marks them published; a failing row is marked failed and stays
        /// in the outbox for the RevocationOutboxPublisher. Never throws (the change is already committed).</summary>
        Task PublishAsync(IReadOnlyList<Domain.Entities.RevocationOutboxMessage> rows, CancellationToken ct);

        /// <summary>Stage + publish without storing - for code paths without a unit of work (and tests). Never throws.</summary>
        Task FlushAsync(CancellationToken ct);

        /// <summary>Drops pending revocations (rollback).</summary>
        void Clear();
    }
}
