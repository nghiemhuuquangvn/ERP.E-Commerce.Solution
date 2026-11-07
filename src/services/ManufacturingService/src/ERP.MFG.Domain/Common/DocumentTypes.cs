namespace ERP.MFG.Domain.Common;

/// <summary>
/// Document types and their number prefixes (BR-GEN-04). A number is <c>{Prefix}-{yyMM}-{seq 5 digits}</c>;
/// the sequence is kept per tenant + document type + plant + month (<c>DocumentSequence</c>).
/// </summary>
public static class DocumentTypes
{
    public const string ProductionOrder = "MO";
    public const string MaterialIssue = "MI";
    public const string MaterialReturn = "MR";
    public const string MaterialRequest = "MQ";
    public const string OperationReport = "OR";
    public const string FinishedGoodsReceipt = "FR";
    public const string Inspection = "QI";
    public const string Ncr = "NCR";
    public const string PlannedOrder = "PLO";
    public const string MrpRun = "MRP";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        ProductionOrder, MaterialIssue, MaterialReturn, MaterialRequest, OperationReport,
        FinishedGoodsReceipt, Inspection, Ncr, PlannedOrder, MrpRun,
    };

    /// <summary>The number text for a sequence value: <c>MO-2611-00001</c>.</summary>
    public static string Format(string docType, DateTime date, long sequence)
        => $"{docType}-{date:yyMM}-{sequence:D5}";
}

/// <summary>A master record that belongs to a plant and has a code unique within it (shift, calendar, work center, machine, standard operation, crew).</summary>
public interface IPlantCoded
{
    Guid PlantId { get; }
    string Code { get; }
}
