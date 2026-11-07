using ERP.MFG.Domain.Common;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>A reason picked when scrapping, reworking, stopping a machine, cancelling or holding an order ... (BR-RSN-01). The code is unique per type within the tenant.</summary>
public class ReasonCode : BaseEntity
{
    /// <summary>The set every shop starts with (T1.5.1). Written once per tenant, the first time reason codes are read or on request; a shop edits or deactivates them freely.</summary>
    public static readonly IReadOnlyList<(ReasonType Type, string Code, string Name, DowntimeCategory? Category)> DefaultSeeds =
    [
        (ReasonType.Scrap, "SCRAP-01", "Lỗi vật liệu", null),
        (ReasonType.Scrap, "SCRAP-02", "Lỗi thao tác", null),
        (ReasonType.Rework, "RWK-01", "Sai kích thước", null),
        (ReasonType.Downtime, "DT-01", "Hỏng máy", DowntimeCategory.Unplanned),
        (ReasonType.Downtime, "DT-02", "Bảo trì định kỳ", DowntimeCategory.Planned),
        (ReasonType.Downtime, "DT-03", "Chờ vật tư", DowntimeCategory.Unplanned),
        (ReasonType.Cancel, "CAN-01", "Khách hủy đơn", null),
        (ReasonType.Hold, "HLD-01", "Chờ vật tư", null),
        (ReasonType.ShortClose, "SC-01", "Thiếu nguyên liệu", null),
        (ReasonType.UnplannedMaterial, "UPM-01", "Bổ sung ngoài định mức", null),
        (ReasonType.OverIssue, "OVI-01", "Hao hụt vượt định mức", null),
    ];

    private ReasonCode() { }

    public ReasonType Type { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DowntimeCategory? Category { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static ReasonCode Create(ReasonType type, string code, string name, DowntimeCategory? category)
    {
        var reason = new ReasonCode { Id = Guid.NewGuid(), Code = MasterCode.Valid(code) };
        reason.Apply(type, name, category);
        return reason;
    }

    /// <summary>The type and the code identify a reason in documents and never change.</summary>
    public void Update(string name, DowntimeCategory? category)
    {
        Apply(Type, name, category);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    /// <summary>BR-RSN-02: a downtime reason is Planned or Unplanned (OEE needs it); other types have no category.</summary>
    private void Apply(ReasonType type, string name, DowntimeCategory? category)
    {
        if (!Enum.IsDefined(type)) throw MfgRules.Invalid("Unknown reason type.");
        Name = MasterCode.Name(name, 200);
        if (type == ReasonType.Downtime && (category is null || !Enum.IsDefined(category.Value)))
            throw MfgRules.Invalid("A downtime reason must be Planned or Unplanned.");
        Type = type;
        Category = type == ReasonType.Downtime ? category : null;
    }
}

/// <summary>A production team (BA spec 01 §5.7 A10) with its members. Members belong to one team at a time (BR-LBR-01).</summary>
public class Crew : BaseEntity, IPlantCoded
{
    private readonly List<CrewMember> _members = [];

    private Crew() { }

    public Guid PlantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid? WorkCenterId { get; private set; }
    public Guid? LeaderEmployeeId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyList<CrewMember> Members => _members;

    public static Crew Create(Guid plantId, string code, string name, Guid? workCenterId, Guid? leaderEmployeeId)
        => new()
        {
            Id = Guid.NewGuid(), PlantId = plantId, Code = MasterCode.Valid(code), Name = MasterCode.Name(name, 200),
            WorkCenterId = Empty(workCenterId), LeaderEmployeeId = Empty(leaderEmployeeId),
        };

    public void Update(string name, Guid? workCenterId, Guid? leaderEmployeeId)
    {
        Name = MasterCode.Name(name, 200);
        WorkCenterId = Empty(workCenterId);
        LeaderEmployeeId = Empty(leaderEmployeeId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    public CrewMember AddMember(Guid employeeId, DateOnly validFrom, DateOnly? validTo, decimal? laborRatePerHour)
    {
        var member = CrewMember.Create(Id, employeeId, validFrom, validTo, laborRatePerHour);
        _members.Add(member);
        UpdatedAt = DateTime.UtcNow;
        return member;
    }

    public bool RemoveMember(Guid memberId)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return false;
        _members.Remove(member);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static Guid? Empty(Guid? id) => id == Guid.Empty ? null : id;
}

/// <summary>An employee in a crew for a period; an own labor rate, when set, wins over the work center's (BR-LBR-02).</summary>
public class CrewMember : BaseEntity
{
    private CrewMember() { }

    public Guid CrewId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public decimal? LaborRatePerHour { get; private set; }

    public static CrewMember Create(Guid crewId, Guid employeeId, DateOnly validFrom, DateOnly? validTo, decimal? laborRatePerHour)
    {
        if (employeeId == Guid.Empty) throw MfgRules.Invalid("A crew member is an employee.");
        if (validTo is not null && validTo < validFrom) throw MfgRules.Invalid("The membership cannot end before it starts.");
        if (laborRatePerHour is < 0) throw MfgRules.Invalid("A labor rate cannot be negative.");
        return new CrewMember { Id = Guid.NewGuid(), CrewId = crewId, EmployeeId = employeeId, ValidFrom = validFrom, ValidTo = validTo, LaborRatePerHour = laborRatePerHour };
    }

    public bool IsActiveOn(DateOnly date) => ValidFrom <= date && (ValidTo is null || date <= ValidTo);

    /// <summary>Do the two periods share a day? (An open end lasts for ever.)</summary>
    public static bool Overlaps(DateOnly fromA, DateOnly? toA, DateOnly fromB, DateOnly? toB)
        => fromA <= (toB ?? DateOnly.MaxValue) && fromB <= (toA ?? DateOnly.MaxValue);
}
