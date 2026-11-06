using ERP.MFG.Domain.Common;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>The editable values of an item's manufacturing profile (BA spec 01 §5.3). Low-level code and standard cost are not here: the system owns them (BR-ITM-04).</summary>
public sealed record ItemProfileValues(
    MakeOrBuy MakeOrBuy,
    int FixedLeadTimeDays,
    int PurchaseLeadTimeDays,
    LotSizingPolicy LotSizingPolicy,
    decimal? FixedLotQty,
    decimal? MinOrderQty,
    decimal? OrderMultiple,
    int? PeriodDays,
    decimal SafetyStockQty,
    decimal StandardLotSize,
    IssueMethod? DefaultIssueMethod,
    bool RequiresFqc,
    Guid? FqcInspectionPlanId,
    int ShelfLifeDays);

/// <summary>
/// What a plant knows about an item beyond the product catalogue: make or buy, lead times, lot-sizing policy, safety stock, FQC, shelf life.
/// One per (item, plant). The item is a ProductService product, referenced by id; the code and name are a snapshot for display.
/// </summary>
public class ItemManufacturingProfile : BaseEntity
{
    private ItemManufacturingProfile() { }

    public Guid ItemId { get; private set; }
    public Guid PlantId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public MakeOrBuy MakeOrBuy { get; private set; } = MakeOrBuy.Make;
    public int FixedLeadTimeDays { get; private set; }
    public int PurchaseLeadTimeDays { get; private set; }
    public LotSizingPolicy LotSizingPolicy { get; private set; } = LotSizingPolicy.LotForLot;
    public decimal? FixedLotQty { get; private set; }
    public decimal? MinOrderQty { get; private set; }
    public decimal? OrderMultiple { get; private set; }
    public int? PeriodDays { get; private set; }
    public decimal SafetyStockQty { get; private set; }
    public decimal StandardLotSize { get; private set; } = 1m;
    public IssueMethod? DefaultIssueMethod { get; private set; }
    public bool RequiresFqc { get; private set; }
    public Guid? FqcInspectionPlanId { get; private set; }
    public int ShelfLifeDays { get; private set; }

    /// <summary>Computed by the system when a BOM is approved or made obsolete (BR-BOM-13); the API never sets it.</summary>
    public int LowLevelCode { get; private set; }

    /// <summary>Set by cost roll-up / entered for bought items in a later phase; the profile API never sets it.</summary>
    public decimal StandardCost { get; private set; }

    public static ItemManufacturingProfile Create(Guid itemId, Guid plantId, string itemCode, string itemName, ItemProfileValues values)
    {
        var profile = new ItemManufacturingProfile { Id = Guid.NewGuid(), ItemId = itemId, PlantId = plantId };
        profile.Update(itemCode, itemName, values);
        return profile;
    }

    /// <summary>The profile an item has before anybody saved one: a made item, lot for lot, no lead time, nothing else (T0.6.3 GetProfileOrDefault).</summary>
    public static ItemProfileValues DefaultValues { get; } = new(MakeOrBuy.Make, 0, 0, LotSizingPolicy.LotForLot, null, null, null, null, 0m, 1m, null, false, null, 0);

    public ItemProfileValues ToValues() => new(MakeOrBuy, FixedLeadTimeDays, PurchaseLeadTimeDays, LotSizingPolicy, FixedLotQty, MinOrderQty,
        OrderMultiple, PeriodDays, SafetyStockQty, StandardLotSize, DefaultIssueMethod, RequiresFqc, FqcInspectionPlanId, ShelfLifeDays);

    public void Update(string itemCode, string itemName, ItemProfileValues v)
    {
        Validate(v);
        ItemCode = itemCode;
        ItemName = itemName;
        MakeOrBuy = v.MakeOrBuy;
        FixedLeadTimeDays = v.FixedLeadTimeDays;
        PurchaseLeadTimeDays = v.PurchaseLeadTimeDays;
        LotSizingPolicy = v.LotSizingPolicy;
        FixedLotQty = v.LotSizingPolicy == LotSizingPolicy.FixedQty ? v.FixedLotQty : null;
        MinOrderQty = v.LotSizingPolicy == LotSizingPolicy.MinMultiple ? v.MinOrderQty : null;
        OrderMultiple = v.LotSizingPolicy == LotSizingPolicy.MinMultiple ? v.OrderMultiple : null;
        PeriodDays = v.LotSizingPolicy == LotSizingPolicy.PeriodOrder ? v.PeriodDays : null;
        SafetyStockQty = v.SafetyStockQty;
        StandardLotSize = v.StandardLotSize;
        DefaultIssueMethod = v.DefaultIssueMethod;
        RequiresFqc = v.RequiresFqc;
        FqcInspectionPlanId = v.RequiresFqc ? v.FqcInspectionPlanId : null;
        ShelfLifeDays = v.ShelfLifeDays;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Used by the BOM approval flow (BR-BOM-13). Not reachable from the profile API.</summary>
    public void SetLowLevelCode(int lowLevelCode) => LowLevelCode = lowLevelCode;

    /// <summary>Used by cost roll-up (BR-CST-01). Not reachable from the profile API.</summary>
    public void SetStandardCost(decimal standardCost) => StandardCost = standardCost;

    /// <summary>BR-ITM-03: the lot-sizing parameters the chosen policy needs (MFG_ITM_002); lead times and safety stock cannot be negative; the standard lot is positive.</summary>
    public static void Validate(ItemProfileValues v)
    {
        if (!Enum.IsDefined(v.MakeOrBuy) || !Enum.IsDefined(v.LotSizingPolicy)) throw MfgRules.Fail(MfgErrors.Itm002);
        if (v.DefaultIssueMethod.HasValue && !Enum.IsDefined(v.DefaultIssueMethod.Value)) throw MfgRules.Invalid("Unknown issue method.");

        switch (v.LotSizingPolicy)
        {
            case LotSizingPolicy.FixedQty when v.FixedLotQty is null or <= 0:
            case LotSizingPolicy.MinMultiple when v.MinOrderQty is null or < 0 || v.OrderMultiple is null or <= 0:
            case LotSizingPolicy.PeriodOrder when v.PeriodDays is null or < 1 or > 90:
                throw MfgRules.Fail(MfgErrors.Itm002);
        }

        if (v.FixedLeadTimeDays < 0 || v.PurchaseLeadTimeDays < 0) throw MfgRules.Invalid("Lead times cannot be negative.");
        if (v.SafetyStockQty < 0) throw MfgRules.Invalid("Safety stock cannot be negative.");
        if (v.StandardLotSize <= 0) throw MfgRules.Invalid("The standard lot size must be greater than 0.");
        if (v.ShelfLifeDays < 0) throw MfgRules.Invalid("Shelf life cannot be negative.");
    }
}
