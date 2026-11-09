namespace ERP.MFG.Application.Abstractions;

/// <summary>Issues the next document number (BR-GEN-04), safe under parallel requests.</summary>
public interface IDocumentNumberService
{
    /// <summary>
    /// The next number of <paramref name="docType"/> (<c>DocumentTypes.*</c>) for <paramref name="plantId"/> in the
    /// month of <paramref name="businessDate"/> (the plant's business date, BR-GEN-05), e.g. <c>MO-2611-00001</c>.
    /// Pass <see cref="Guid.Empty"/> as the plant for a document whose number must be unique in the whole tenant (production orders: the number is the idempotency key of the stock
    /// movements in the Warehouse, so two plants must never issue the same one - a deviation from BR-GEN-04, which counts per plant).
    /// Runs on the caller's database connection, so a number is only used when the caller's transaction commits
    /// (a rolled-back request leaves no gap).
    /// </summary>
    Task<string> NextAsync(string docType, Guid plantId, DateTime businessDate, CancellationToken ct = default);
}
