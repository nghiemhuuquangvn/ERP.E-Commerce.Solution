using System.Text.RegularExpressions;
using ERP.MFG.Domain.Common;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>
/// A production site (BA spec 01 §5.2). Its code is unique per tenant, 2-20 characters of A-Z 0-9 '-', stored upper case (BR-PLT-01);
/// it names the default raw-material, finished-goods and quarantine warehouses, and optionally a scrap warehouse (BR-PLT-02), and the
/// default work calendar used by MRP and by orders without a routing (BR-PLT-03). The calendar is empty until calendars exist (M2) and is
/// required when an order is released (Phase 3).
/// </summary>
public class Plant : BaseEntity
{
    public const string DefaultTimeZone = "Asia/Ho_Chi_Minh";
    private static readonly Regex CodePattern = new("^[A-Z0-9-]{2,20}$", RegexOptions.Compiled);

    private Plant() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = DefaultTimeZone;
    public Guid RawMaterialWarehouseId { get; private set; }
    public Guid FinishedGoodsWarehouseId { get; private set; }
    public Guid QuarantineWarehouseId { get; private set; }
    public Guid? ScrapWarehouseId { get; private set; }
    public Guid? DefaultCalendarId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>The normal form of a code: trimmed and upper case (so "pl01" and "PL01" are the same plant).</summary>
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static Plant Create(string code, string name, string? timeZoneId, Guid rawMaterialWarehouseId, Guid finishedGoodsWarehouseId,
        Guid quarantineWarehouseId, Guid? scrapWarehouseId, Guid? defaultCalendarId)
    {
        var plant = new Plant { Id = Guid.NewGuid(), Code = ValidCode(code) };
        plant.Apply(name, timeZoneId, rawMaterialWarehouseId, finishedGoodsWarehouseId, quarantineWarehouseId, scrapWarehouseId, defaultCalendarId);
        return plant;
    }

    /// <summary>Changes everything but the code (a code identifies the plant in documents and is never edited).</summary>
    public void Update(string name, string? timeZoneId, Guid rawMaterialWarehouseId, Guid finishedGoodsWarehouseId,
        Guid quarantineWarehouseId, Guid? scrapWarehouseId, Guid? defaultCalendarId)
    {
        Apply(name, timeZoneId, rawMaterialWarehouseId, finishedGoodsWarehouseId, quarantineWarehouseId, scrapWarehouseId, defaultCalendarId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string ValidCode(string code)
    {
        var normal = NormalizeCode(code);
        if (!CodePattern.IsMatch(normal))
            throw MfgRules.Invalid("Plant code must be 2-20 characters: letters A-Z, digits 0-9 and '-'.");
        return normal;
    }

    private void Apply(string name, string? timeZoneId, Guid raw, Guid finished, Guid quarantine, Guid? scrap, Guid? calendar)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > 200) throw MfgRules.Invalid("Plant name is required (up to 200 characters).");
        if (raw == Guid.Empty || finished == Guid.Empty || quarantine == Guid.Empty)
            throw MfgRules.Invalid("A plant needs a raw-material, a finished-goods and a quarantine warehouse.");

        var zone = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZone : timeZoneId.Trim();
        if (!IsKnownTimeZone(zone)) throw MfgRules.Invalid($"Unknown time zone '{zone}'.");

        Name = trimmed;
        TimeZoneId = zone;
        RawMaterialWarehouseId = raw;
        FinishedGoodsWarehouseId = finished;
        QuarantineWarehouseId = quarantine;
        ScrapWarehouseId = scrap == Guid.Empty ? null : scrap;
        DefaultCalendarId = calendar == Guid.Empty ? null : calendar;
    }

    private static bool IsKnownTimeZone(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    /// <summary>All warehouse ids of the plant (scrap only when set) - what the Warehouse service must confirm.</summary>
    public IReadOnlyList<Guid> WarehouseIds()
    {
        var ids = new List<Guid> { RawMaterialWarehouseId, FinishedGoodsWarehouseId, QuarantineWarehouseId };
        if (ScrapWarehouseId.HasValue) ids.Add(ScrapWarehouseId.Value);
        return ids.Distinct().ToList();
    }
}
