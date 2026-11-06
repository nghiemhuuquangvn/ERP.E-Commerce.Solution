using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>
/// Last used number of one document type in one plant and month (BR-GEN-04). Never edited by hand: the
/// database increments it in a single atomic statement (see <c>DocumentNumberService</c>), so parallel
/// requests never get the same number. Numbers are unique, not necessarily gap free.
/// </summary>
public class DocumentSequence : BaseEntity
{
    private DocumentSequence() { }

    public static DocumentSequence Create(string docType, Guid plantId, DateOnly period) =>
        new() { DocType = docType, PlantId = plantId, Period = new DateOnly(period.Year, period.Month, 1), LastValue = 1 };

    public string DocType { get; private set; } = string.Empty;

    /// <summary>The plant the number belongs to; <see cref="Guid.Empty"/> for documents without a plant.</summary>
    public Guid PlantId { get; private set; }

    /// <summary>First day of the month the sequence counts in (the plant's business date).</summary>
    public DateOnly Period { get; private set; }

    public long LastValue { get; private set; }
}
