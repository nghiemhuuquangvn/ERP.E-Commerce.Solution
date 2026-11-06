using ERP.MFG.Domain.Common;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>The editable values of a plant's production parameters (BA spec 01 §5.1).</summary>
public sealed record ProductionSettingValues(
    decimal OverProductionTolerancePct,
    decimal UnderCompletionTolerancePct,
    decimal OverIssueTolerancePct,
    bool BlockReleaseOnShortage,
    bool AllowNegativeStockOnBackflush,
    bool RequireSequentialOperations,
    bool AutoCompleteOnFullReceipt,
    IssueMethod DefaultIssueMethod,
    CostingMethod CostingMethod,
    int MrpHorizonDays,
    int MaxBomLevels,
    string FgLotNumberFormat)
{
    public const string DefaultLotFormat = "{ItemCode}-{yyMMdd}-{seq2}";

    /// <summary>The values a new plant starts with (BR-SET-01).</summary>
    public static ProductionSettingValues Defaults { get; } = new(0, 0, 0, false, false, true, true, IssueMethod.Manual, CostingMethod.Actual, 90, 20, DefaultLotFormat);
}

/// <summary>One changed field of a settings update, for the audit trail (BR-SET-02).</summary>
public sealed record SettingChange(string Field, string Old, string New);

/// <summary>
/// The production parameters of one plant - exactly one row per plant, created with the plant (BR-SET-01). A change applies to
/// transactions after it is saved, never retroactively; the old and new value of every changed field are audited (BR-SET-02).
/// </summary>
public class ProductionSetting : BaseEntity
{
    private ProductionSetting() { }

    public Guid PlantId { get; private set; }
    public decimal OverProductionTolerancePct { get; private set; }
    public decimal UnderCompletionTolerancePct { get; private set; }
    public decimal OverIssueTolerancePct { get; private set; }
    public bool BlockReleaseOnShortage { get; private set; }
    public bool AllowNegativeStockOnBackflush { get; private set; }
    public bool RequireSequentialOperations { get; private set; } = true;
    public bool AutoCompleteOnFullReceipt { get; private set; } = true;
    public IssueMethod DefaultIssueMethod { get; private set; } = IssueMethod.Manual;
    public CostingMethod CostingMethod { get; private set; } = CostingMethod.Actual;
    public int MrpHorizonDays { get; private set; } = 90;
    public int MaxBomLevels { get; private set; } = 20;
    public string FgLotNumberFormat { get; private set; } = ProductionSettingValues.DefaultLotFormat;

    public static ProductionSetting CreateDefault(Guid plantId)
    {
        var setting = new ProductionSetting { Id = Guid.NewGuid(), PlantId = plantId };
        setting.Assign(ProductionSettingValues.Defaults);
        return setting;
    }

    public ProductionSettingValues ToValues() => new(OverProductionTolerancePct, UnderCompletionTolerancePct, OverIssueTolerancePct,
        BlockReleaseOnShortage, AllowNegativeStockOnBackflush, RequireSequentialOperations, AutoCompleteOnFullReceipt,
        DefaultIssueMethod, CostingMethod, MrpHorizonDays, MaxBomLevels, FgLotNumberFormat);

    /// <summary>
    /// Validates and applies the new values; returns the list of fields that really changed (empty = nothing to save).
    /// Changing <see cref="CostingMethod"/> is refused while production orders are open (BR-SET-03): <paramref name="openOrdersExist"/>
    /// tells whether any order is Released / InProgress / Completed-not-closed.
    /// </summary>
    public IReadOnlyList<SettingChange> Update(ProductionSettingValues next, bool openOrdersExist)
    {
        Validate(next);
        if (next.CostingMethod != CostingMethod && openOrdersExist)
            throw MfgRules.Fail(MfgErrors.Set001);

        var changes = Diff(ToValues(), next);
        if (changes.Count == 0) return changes;
        Assign(next);
        UpdatedAt = DateTime.UtcNow;
        return changes;
    }

    private void Assign(ProductionSettingValues v)
    {
        Validate(v);
        OverProductionTolerancePct = v.OverProductionTolerancePct;
        UnderCompletionTolerancePct = v.UnderCompletionTolerancePct;
        OverIssueTolerancePct = v.OverIssueTolerancePct;
        BlockReleaseOnShortage = v.BlockReleaseOnShortage;
        AllowNegativeStockOnBackflush = v.AllowNegativeStockOnBackflush;
        RequireSequentialOperations = v.RequireSequentialOperations;
        AutoCompleteOnFullReceipt = v.AutoCompleteOnFullReceipt;
        DefaultIssueMethod = v.DefaultIssueMethod;
        CostingMethod = v.CostingMethod;
        MrpHorizonDays = v.MrpHorizonDays;
        MaxBomLevels = v.MaxBomLevels;
        FgLotNumberFormat = v.FgLotNumberFormat.Trim();
    }

    /// <summary>Ranges of BA spec §5.1: tolerances 0-100, MRP horizon 7-365 (MFG_MRP_002), BOM levels 1-50, lot format not empty.</summary>
    public static void Validate(ProductionSettingValues v)
    {
        foreach (var (name, value) in new[]
                 {
                     ("OverProductionTolerancePct", v.OverProductionTolerancePct),
                     ("UnderCompletionTolerancePct", v.UnderCompletionTolerancePct),
                     ("OverIssueTolerancePct", v.OverIssueTolerancePct),
                 })
        {
            if (value is < 0 or > 100) throw MfgRules.Invalid($"{name} must be between 0 and 100.");
        }

        if (v.MrpHorizonDays is < 7 or > 365) throw MfgRules.Fail(MfgErrors.Mrp002);
        if (v.MaxBomLevels is < 1 or > 50) throw MfgRules.Invalid("MaxBomLevels must be between 1 and 50.");
        if (string.IsNullOrWhiteSpace(v.FgLotNumberFormat) || v.FgLotNumberFormat.Length > 100)
            throw MfgRules.Invalid("The finished-goods lot number format is required (up to 100 characters).");
        if (!Enum.IsDefined(v.DefaultIssueMethod) || !Enum.IsDefined(v.CostingMethod))
            throw MfgRules.Invalid("Unknown issue or costing method.");
    }

    private static List<SettingChange> Diff(ProductionSettingValues a, ProductionSettingValues b)
    {
        var changes = new List<SettingChange>();
        // decimals without trailing zeros (PostgreSQL returns 0.00 for numeric(5,2)), so the audit reads 0 -> 7
        static string Text<T>(T value) => value is decimal d ? d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        void Add<T>(string field, T old, T next)
        {
            if (!EqualityComparer<T>.Default.Equals(old, next))
                changes.Add(new SettingChange(field, Text(old), Text(next)));
        }

        Add(nameof(a.OverProductionTolerancePct), a.OverProductionTolerancePct, b.OverProductionTolerancePct);
        Add(nameof(a.UnderCompletionTolerancePct), a.UnderCompletionTolerancePct, b.UnderCompletionTolerancePct);
        Add(nameof(a.OverIssueTolerancePct), a.OverIssueTolerancePct, b.OverIssueTolerancePct);
        Add(nameof(a.BlockReleaseOnShortage), a.BlockReleaseOnShortage, b.BlockReleaseOnShortage);
        Add(nameof(a.AllowNegativeStockOnBackflush), a.AllowNegativeStockOnBackflush, b.AllowNegativeStockOnBackflush);
        Add(nameof(a.RequireSequentialOperations), a.RequireSequentialOperations, b.RequireSequentialOperations);
        Add(nameof(a.AutoCompleteOnFullReceipt), a.AutoCompleteOnFullReceipt, b.AutoCompleteOnFullReceipt);
        Add(nameof(a.DefaultIssueMethod), a.DefaultIssueMethod, b.DefaultIssueMethod);
        Add(nameof(a.CostingMethod), a.CostingMethod, b.CostingMethod);
        Add(nameof(a.MrpHorizonDays), a.MrpHorizonDays, b.MrpHorizonDays);
        Add(nameof(a.MaxBomLevels), a.MaxBomLevels, b.MaxBomLevels);
        Add(nameof(a.FgLotNumberFormat), a.FgLotNumberFormat, b.FgLotNumberFormat.Trim());
        return changes;
    }
}

/// <summary>An audit row of a settings change: who changed which field from what to what (BR-SET-02, NFR-06).</summary>
public class ProductionSettingAudit : BaseEntity
{
    private ProductionSettingAudit() { }

    public Guid PlantId { get; private set; }
    public string Field { get; private set; } = string.Empty;
    public string OldValue { get; private set; } = string.Empty;
    public string NewValue { get; private set; } = string.Empty;
    public string ChangedBy { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }

    public static ProductionSettingAudit Record(Guid plantId, SettingChange change, string changedBy) => new()
    {
        Id = Guid.NewGuid(),
        PlantId = plantId,
        Field = change.Field,
        OldValue = change.Old,
        NewValue = change.New,
        ChangedBy = changedBy,
        ChangedAt = DateTime.UtcNow,
    };
}
