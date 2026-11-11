using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Application.Abstractions;

/// <summary>Storage of the plant-scoped master records of M2 (shifts, work centers, machines, standard operations ...). Reads see only the current tenant.</summary>
public interface IPlantCatalog
{
    Task<T?> GetAsync<T>(Guid id, CancellationToken ct = default) where T : BaseEntity, IPlantCoded;
    Task<List<T>> ListAsync<T>(Guid plantId, CancellationToken ct = default) where T : BaseEntity, IPlantCoded;
    Task<List<T>> GetManyAsync<T>(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) where T : BaseEntity, IPlantCoded;
    Task<bool> CodeExistsAsync<T>(Guid plantId, string normalizedCode, CancellationToken ct = default) where T : BaseEntity, IPlantCoded;
    void Add<T>(T entity) where T : BaseEntity, IPlantCoded;
}

public interface ICalendarRepository
{
    /// <summary>The calendar with its weekly pattern and exceptions.</summary>
    Task<WorkCalendar?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<WorkCalendar>> ListAsync(Guid plantId, CancellationToken ct = default);
    Task<List<WorkCalendar>> UsingShiftAsync(Guid shiftId, CancellationToken ct = default);
}

public interface ICrewRepository
{
    Task<Crew?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<Crew>> ListAsync(Guid plantId, CancellationToken ct = default);

    /// <summary>BR-LBR-01: the members of any crew (other than <paramref name="exceptMemberId"/>) whose period shares a day with the given one.</summary>
    Task<bool> HasOverlappingMembershipAsync(Guid employeeId, DateOnly from, DateOnly? to, Guid? exceptMemberId, CancellationToken ct = default);

    /// <summary>BR-LBR-02: the labor rate of the crew membership of an employee that is valid on a date, if it has its own rate.</summary>
    Task<decimal?> OwnRateOnAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);
}

public interface IReasonCodeRepository
{
    Task<ReasonCode?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<ReasonCode>> ListAsync(ReasonType? type, CancellationToken ct = default);
    Task<bool> ExistsAsync(ReasonType type, string normalizedCode, CancellationToken ct = default);
    void Add(ReasonCode reason);
}

/// <summary>
/// BR-GEN-02: a master record that is used elsewhere can be deactivated but not deleted (<c>MFG_GEN_005</c>). Later phases add routing, BOM and
/// order references to these checks.
/// </summary>
public interface IReferenceChecker
{
    Task<bool> ShiftIsUsedAsync(Guid shiftId, CancellationToken ct = default);
    Task<bool> CalendarIsUsedAsync(Guid calendarId, CancellationToken ct = default);
    Task<bool> WorkCenterIsUsedAsync(Guid workCenterId, CancellationToken ct = default);
    Task<bool> MachineIsUsedAsync(Guid machineId, CancellationToken ct = default);
    Task<bool> StandardOperationIsUsedAsync(Guid standardOperationId, CancellationToken ct = default);
    Task<bool> ReasonIsUsedAsync(Guid reasonId, CancellationToken ct = default);
    Task<bool> CrewIsUsedAsync(Guid crewId, CancellationToken ct = default);

    /// <summary>Is an inspection plan (any version of its series) pointed at by an item profile, a routing operation, an order operation or an inspection (P4-3)?</summary>
    Task<bool> InspectionPlanIsUsedAsync(Guid planId, CancellationToken ct = default);
}

/// <summary>The working-time arithmetic of one calendar (BR-CAL-05); calendars are cached for 60 s per tenant and dropped when changed.</summary>
public interface IWorkingTimeService
{
    Task<WorkingTimeCalculator> ForCalendarAsync(Guid calendarId, CancellationToken ct = default);
    void Invalidate(Guid calendarId);
}

public sealed record DayCapacity(DateOnly Date, int WorkingMinutes, decimal CapacityMinutes);

public interface ICapacityService
{
    /// <summary>BR-WC-02: per day from..to (inclusive) the working minutes of the work center's calendar and its standard-minute capacity.</summary>
    Task<IReadOnlyList<DayCapacity>> GetDailyCapacityAsync(Guid workCenterId, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public interface ILaborRateResolver
{
    /// <summary>BR-LBR-02: the employee's own rate of the crew membership valid on the date, else the work center's labor rate.</summary>
    Task<decimal> GetRateAsync(Guid employeeId, Guid workCenterId, DateOnly date, CancellationToken ct = default);
}

/// <summary>Employee facts (MasterData <c>internal/v1/employees/lookup</c>).</summary>
public sealed record EmployeeInfo(Guid Id, string EmployeeNumber, string FullName, bool IsActive);

public interface IEmployeeLookupClient
{
    Task<IReadOnlyList<EmployeeInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
