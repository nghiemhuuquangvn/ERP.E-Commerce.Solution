using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Posting
{
    /// <summary>
    /// One line of a generated entry. Either a <see cref="PostingKey"/> (resolved through the tenant's posting profile) or an
    /// explicit account; exactly one amount is positive.
    /// </summary>
    public sealed record PostingLine(string? PostingKey, Guid? AccountId, decimal Debit, decimal Credit, string Description = "")
    {
        public static PostingLine Dr(string postingKey, decimal amount, string description = "") =>
            new(postingKey, null, amount, 0, description);

        public static PostingLine Cr(string postingKey, decimal amount, string description = "") =>
            new(postingKey, null, 0, amount, description);
    }

    /// <summary>A business document that must reach the ledger: what happened, when, and the lines.</summary>
    public sealed record PostingRequest(
        string SourceType,
        Guid SourceId,
        DateTime Date,
        string Description,
        IReadOnlyList<PostingLine> Lines);

    /// <summary>Per-tenant, gap-tolerant journal numbers ("JE-2026-000001").</summary>
    public interface IJournalNumberGenerator
    {
        Task<string> NextAsync(Guid tenantId, int year, CancellationToken ct = default);
    }

    /// <summary>
    /// Writes journal entries for business documents. Everything is staged on the caller's unit of work (the document, the
    /// entry and the outbox rows commit together); the caller saves. Idempotent per (SourceType, SourceId).
    /// </summary>
    public interface IJournalPostingService
    {
        /// <summary>
        /// Posts the request, or returns the entry already posted for the same source. Null when automatic posting is
        /// switched off. Throws <see cref="ERP.ACC.Domain.PostingConfigurationException"/> when the posting profile is
        /// incomplete.
        /// </summary>
        Task<JournalEntry?> PostAsync(PostingRequest request, CancellationToken ct = default);

        /// <summary>
        /// Reverses the entry posted for the source (a mirror entry; the original stays in the books). Null when there is
        /// nothing to reverse (never posted, already reversed, or automatic posting is off).
        /// </summary>
        Task<JournalEntry?> ReverseAsync(string sourceType, Guid sourceId, string reason, DateTime date, CancellationToken ct = default);
    }
}
