using ERP.MFG.Domain.Common;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>The editable values of a work center (BA spec 01 §5.5).</summary>
public sealed record WorkCenterValues(
    string Name,
    Guid? DepartmentId,
    Guid CalendarId,
    WorkCenterType Type,
    int NumberOfResources,
    decimal EfficiencyPct,
    decimal UtilizationPct,
    decimal LaborRatePerHour,
    decimal MachineRatePerHour,
    int QueueTimeMinutes,
    int MoveTimeMinutes,
    Guid? VendorId);

/// <summary>A place where operations are done: a machine group, a labour station or a subcontractor. It owns the capacity and the rates used by scheduling and costing.</summary>
public class WorkCenter : BaseEntity, IPlantCoded
{
    private WorkCenter() { }

    public Guid PlantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid? DepartmentId { get; private set; }
    public Guid CalendarId { get; private set; }
    public WorkCenterType Type { get; private set; }
    public int NumberOfResources { get; private set; } = 1;
    public decimal EfficiencyPct { get; private set; } = 100m;
    public decimal UtilizationPct { get; private set; } = 100m;
    public decimal LaborRatePerHour { get; private set; }
    public decimal MachineRatePerHour { get; private set; }
    public int QueueTimeMinutes { get; private set; }
    public int MoveTimeMinutes { get; private set; }
    public Guid? VendorId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static WorkCenter Create(Guid plantId, string code, WorkCenterValues values)
    {
        var center = new WorkCenter { Id = Guid.NewGuid(), PlantId = plantId, Code = MasterCode.Valid(code, MfgErrors.Wc001) };
        center.Apply(values);
        return center;
    }

    public void Update(WorkCenterValues values)
    {
        Apply(values);
        UpdatedAt = DateTime.UtcNow;
    }

    public WorkCenterValues ToValues() => new(Name, DepartmentId, CalendarId, Type, NumberOfResources, EfficiencyPct, UtilizationPct,
        LaborRatePerHour, MachineRatePerHour, QueueTimeMinutes, MoveTimeMinutes, VendorId);

    /// <summary>
    /// Deactivating is refused while operations of released / in-progress / on-hold orders are on it (BR-WC-03, <c>MFG_WC_003</c>):
    /// <paramref name="hasOpenOperations"/> comes from the order guard. Activating is always allowed.
    /// </summary>
    public void SetActive(bool active, bool hasOpenOperations)
    {
        if (!active && hasOpenOperations) throw MfgRules.Fail(MfgErrors.Wc003);
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    /// <summary>
    /// BR-WC-02: the standard minutes a day offers = working minutes x resources x efficiency% x utilization%.
    /// Example: 900 x 2 x 100% x 90% = 1620.
    /// </summary>
    public decimal DailyCapacity(int workingMinutes) => workingMinutes * NumberOfResources * (EfficiencyPct / 100m) * (UtilizationPct / 100m);

    private void Apply(WorkCenterValues v)
    {
        Name = MasterCode.Name(v.Name, 200);
        if (!Enum.IsDefined(v.Type)) throw MfgRules.Invalid("Unknown work center type.");
        if (v.NumberOfResources is < 1 or > 999 || v.EfficiencyPct is < 1 or > 200 || v.UtilizationPct is < 1 or > 100
            || v.LaborRatePerHour < 0 || v.MachineRatePerHour < 0 || v.QueueTimeMinutes < 0 || v.MoveTimeMinutes < 0)
            throw MfgRules.Fail(MfgErrors.Wc002);
        if (v.Type == WorkCenterType.Subcontract && (v.VendorId is null || v.VendorId == Guid.Empty)) throw MfgRules.Fail(MfgErrors.Wc004);
        if (v.CalendarId == Guid.Empty) throw MfgRules.Invalid("A work center needs a work calendar.");

        DepartmentId = v.DepartmentId == Guid.Empty ? null : v.DepartmentId;
        CalendarId = v.CalendarId;
        Type = v.Type;
        NumberOfResources = v.NumberOfResources;
        EfficiencyPct = v.EfficiencyPct;
        UtilizationPct = v.UtilizationPct;
        LaborRatePerHour = v.LaborRatePerHour;
        MachineRatePerHour = v.MachineRatePerHour;
        QueueTimeMinutes = v.QueueTimeMinutes;
        MoveTimeMinutes = v.MoveTimeMinutes;
        VendorId = v.Type == WorkCenterType.Subcontract ? v.VendorId : null;
    }
}

/// <summary>A machine of a work center; its ideal cycle time feeds OEE (BR-MCH-01, BR-KPI-01).</summary>
public class Machine : BaseEntity, IPlantCoded
{
    private Machine() { }

    public Guid PlantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid WorkCenterId { get; private set; }
    public MachineStatus Status { get; private set; } = MachineStatus.Active;
    public decimal IdealCycleTimeSeconds { get; private set; }
    public string? SerialNo { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Machine Create(Guid plantId, string code, string name, Guid workCenterId, decimal idealCycleTimeSeconds, string? serialNo)
    {
        var machine = new Machine { Id = Guid.NewGuid(), PlantId = plantId, Code = MasterCode.Valid(code) };
        machine.Apply(name, workCenterId, idealCycleTimeSeconds, serialNo);
        return machine;
    }

    public void Update(string name, Guid workCenterId, decimal idealCycleTimeSeconds, string? serialNo)
    {
        Apply(name, workCenterId, idealCycleTimeSeconds, serialNo);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStatus(MachineStatus status)
    {
        if (!Enum.IsDefined(status)) throw MfgRules.Invalid("Unknown machine status.");
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    /// <summary>
    /// BR-MCH-02: a Retired machine cannot be chosen when reporting an operation or a downtime (<c>MFG_OP_010</c>). A machine under maintenance
    /// can be chosen; the returned flag tells the caller to show a warning.
    /// </summary>
    public bool AssertUsable()
    {
        if (Status == MachineStatus.Retired) throw MfgRules.Fail(MfgErrors.Op010, ("machine", Code));
        return Status == MachineStatus.UnderMaintenance;
    }

    private void Apply(string name, Guid workCenterId, decimal idealCycleTimeSeconds, string? serialNo)
    {
        Name = MasterCode.Name(name, 200);
        if (workCenterId == Guid.Empty) throw MfgRules.Invalid("A machine belongs to a work center.");
        if (idealCycleTimeSeconds <= 0) throw MfgRules.Invalid("The ideal cycle time must be greater than 0.");
        WorkCenterId = workCenterId;
        IdealCycleTimeSeconds = idealCycleTimeSeconds;
        SerialNo = string.IsNullOrWhiteSpace(serialNo) ? null : serialNo.Trim();
    }
}

/// <summary>A reusable operation template; putting it into a routing copies its values, the link is not live (BR-OPS-01).</summary>
public class StandardOperation : BaseEntity, IPlantCoded
{
    private StandardOperation() { }

    public Guid PlantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid DefaultWorkCenterId { get; private set; }
    public decimal SetupTimeMinutes { get; private set; }
    public decimal RunTimeMinutes { get; private set; }
    public RunTimeBasis RunTimeBasis { get; private set; } = RunTimeBasis.PerUnit;
    public decimal? BatchSize { get; private set; }
    public string? Instructions { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static StandardOperation Create(Guid plantId, string code, string name, Guid workCenterId, decimal setup, decimal run, RunTimeBasis basis, decimal? batchSize, string? instructions)
    {
        var op = new StandardOperation { Id = Guid.NewGuid(), PlantId = plantId, Code = MasterCode.Valid(code) };
        op.Apply(name, workCenterId, setup, run, basis, batchSize, instructions);
        return op;
    }

    public void Update(string name, Guid workCenterId, decimal setup, decimal run, RunTimeBasis basis, decimal? batchSize, string? instructions)
    {
        Apply(name, workCenterId, setup, run, basis, batchSize, instructions);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    /// <summary>BR-RT-02 for operations: setup >= 0, run > 0, a batch size when the time is per batch (<c>MFG_RT_002</c>).</summary>
    private void Apply(string name, Guid workCenterId, decimal setup, decimal run, RunTimeBasis basis, decimal? batchSize, string? instructions)
    {
        Name = MasterCode.Name(name, 200);
        if (workCenterId == Guid.Empty) throw MfgRules.Invalid("A standard operation needs a default work center.");
        if (setup < 0 || run <= 0 || !Enum.IsDefined(basis) || (basis == RunTimeBasis.PerBatch && batchSize is null or <= 0))
            throw MfgRules.Fail(MfgErrors.Rt002);
        DefaultWorkCenterId = workCenterId;
        SetupTimeMinutes = setup;
        RunTimeMinutes = run;
        RunTimeBasis = basis;
        BatchSize = basis == RunTimeBasis.PerBatch ? batchSize : null;
        Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions;
    }
}
