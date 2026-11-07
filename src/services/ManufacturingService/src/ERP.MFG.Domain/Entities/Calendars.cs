using System.Text.RegularExpressions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>Codes of master data (shift, calendar, work center, machine ... ) are 2-20 characters of A-Z 0-9 '-' and stored upper case.</summary>
public static class MasterCode
{
    private static readonly Regex Pattern = new("^[A-Z0-9-]{2,20}$", RegexOptions.Compiled);

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>The normal form of <paramref name="code"/>, or a refusal with <paramref name="errorCode"/> (the spec's own code for work centers, <c>MFG_VALIDATION</c> otherwise).</summary>
    public static string Valid(string? code, string? errorCode = null)
    {
        var normal = Normalize(code);
        if (Pattern.IsMatch(normal)) return normal;
        throw errorCode is null
            ? MfgRules.Invalid("A code must be 2-20 characters: letters A-Z, digits 0-9 and '-'.")
            : MfgRules.Fail(errorCode);
    }

    public static string Name(string? name, int max = 200)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is 0 || trimmed.Length > max ? throw MfgRules.Invalid($"A name is required (up to {max} characters).") : trimmed;
    }
}

/// <summary>A work shift of a plant: start, end (an end not after the start is an overnight shift) and an unpaid break (BR-CAL-01).</summary>
public class Shift : BaseEntity, IPlantCoded
{
    private Shift() { }

    public Guid PlantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int BreakMinutes { get; private set; }
    public bool IsActive { get; private set; } = true;

    public int RawMinutes => ShiftMath.RawMinutes(StartTime, EndTime);
    public int DurationMinutes => RawMinutes - BreakMinutes;

    public static Shift Create(Guid plantId, string code, string name, TimeOnly start, TimeOnly end, int breakMinutes)
    {
        var shift = new Shift { Id = Guid.NewGuid(), PlantId = plantId, Code = MasterCode.Valid(code) };
        shift.Apply(name, start, end, breakMinutes);
        return shift;
    }

    public void Update(string name, TimeOnly start, TimeOnly end, int breakMinutes)
    {
        Apply(name, start, end, breakMinutes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    public ShiftInfo ToInfo() => new(Id, Code, StartTime, EndTime, BreakMinutes);

    private void Apply(string name, TimeOnly start, TimeOnly end, int breakMinutes)
    {
        Name = MasterCode.Name(name, 100);
        ShiftMath.EnsureValid(start, end, breakMinutes);
        StartTime = start;
        EndTime = end;
        BreakMinutes = breakMinutes;
    }
}

/// <summary>The shifts of one weekday in a calendar's weekly pattern.</summary>
public class WorkCalendarPattern : BaseEntity
{
    private WorkCalendarPattern() { }

    public Guid CalendarId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public Guid ShiftId { get; private set; }

    public static WorkCalendarPattern Create(Guid calendarId, DayOfWeek day, Guid shiftId)
        => new() { Id = Guid.NewGuid(), CalendarId = calendarId, DayOfWeek = day, ShiftId = shiftId };
}

/// <summary>A date that differs from the weekly pattern: a holiday (no work), an extra working day or a day with other shifts (BR-CAL-03, BR-CAL-04).</summary>
public class WorkCalendarException : BaseEntity
{
    private readonly List<WorkCalendarExceptionShift> _shifts = [];

    private WorkCalendarException() { }

    public Guid CalendarId { get; private set; }
    public DateOnly Date { get; private set; }
    public CalendarExceptionType Type { get; private set; }
    public IReadOnlyList<WorkCalendarExceptionShift> Shifts => _shifts;

    public static WorkCalendarException Create(Guid calendarId, DateOnly date, CalendarExceptionType type, string? description, IEnumerable<Guid> shiftIds)
    {
        var exception = new WorkCalendarException { Id = Guid.NewGuid(), CalendarId = calendarId, Date = date, Type = type, Description = description };
        foreach (var shiftId in shiftIds.Distinct())
            exception._shifts.Add(WorkCalendarExceptionShift.Create(exception.Id, shiftId));
        return exception;
    }
}

public class WorkCalendarExceptionShift : BaseEntity
{
    private WorkCalendarExceptionShift() { }

    public Guid ExceptionId { get; private set; }
    public Guid ShiftId { get; private set; }

    public static WorkCalendarExceptionShift Create(Guid exceptionId, Guid shiftId) => new() { Id = Guid.NewGuid(), ExceptionId = exceptionId, ShiftId = shiftId };
}

/// <summary>A working calendar: a weekly pattern of shifts plus exceptions (holidays, extra days). Aggregate root; the shifts are referenced by id.</summary>
public class WorkCalendar : BaseEntity, IPlantCoded
{
    private readonly List<WorkCalendarPattern> _patterns = [];
    private readonly List<WorkCalendarException> _exceptions = [];

    private WorkCalendar() { }

    public Guid PlantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyList<WorkCalendarPattern> Patterns => _patterns;
    public IReadOnlyList<WorkCalendarException> Exceptions => _exceptions;

    public static WorkCalendar Create(Guid plantId, string code, string name)
        => new() { Id = Guid.NewGuid(), PlantId = plantId, Code = MasterCode.Valid(code), Name = MasterCode.Name(name, 100) };

    public void Rename(string name) { Name = MasterCode.Name(name, 100); UpdatedAt = DateTime.UtcNow; }
    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
    public void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    /// <summary>
    /// Replaces the shifts of one weekday. BR-CAL-02: shifts of one day may not overlap, an overnight shift counting into the next day
    /// (<c>MFG_CAL_002</c> names both shifts); shifts that touch (06-14 and 14-22) are fine.
    /// </summary>
    public IReadOnlyList<WorkCalendarPattern> SetDayPattern(DayOfWeek day, IReadOnlyCollection<ShiftInfo> shifts)
    {
        EnsureNoOverlap(shifts);
        _patterns.RemoveAll(p => p.DayOfWeek == day);
        var added = shifts.Select(s => WorkCalendarPattern.Create(Id, day, s.Id)).ToList();
        _patterns.AddRange(added);
        UpdatedAt = DateTime.UtcNow;
        return added;
    }

    /// <summary>BR-CAL-04: one exception per date. A Holiday has no shifts; the other types need at least one, not overlapping.</summary>
    public WorkCalendarException AddException(DateOnly date, CalendarExceptionType type, string? description, IReadOnlyCollection<ShiftInfo> shifts)
    {
        if (_exceptions.Any(e => e.Date == date)) throw MfgRules.Fail(MfgErrors.Gen006, ("code", date.ToString("yyyy-MM-dd")));
        if (!Enum.IsDefined(type)) throw MfgRules.Invalid("Unknown exception type.");
        if (type == CalendarExceptionType.Holiday && shifts.Count > 0) throw MfgRules.Invalid("A holiday has no shifts.");
        if (type != CalendarExceptionType.Holiday && shifts.Count == 0) throw MfgRules.Invalid("An extra or modified working day needs at least one shift.");
        EnsureNoOverlap(shifts);

        var exception = WorkCalendarException.Create(Id, date, type, description, shifts.Select(s => s.Id));
        _exceptions.Add(exception);
        UpdatedAt = DateTime.UtcNow;
        return exception;
    }

    public WorkCalendarException? RemoveException(DateOnly date)
    {
        var exception = _exceptions.FirstOrDefault(e => e.Date == date);
        if (exception is not null) { _exceptions.Remove(exception); UpdatedAt = DateTime.UtcNow; }
        return exception;
    }

    /// <summary>After a shift's times changed: do the shifts of any weekday or exception of this calendar overlap now (BR-CAL-02)?</summary>
    public void Revalidate(IReadOnlyDictionary<Guid, ShiftInfo> shifts)
    {
        ShiftInfo Of(Guid id) => shifts[id];
        foreach (var day in _patterns.GroupBy(p => p.DayOfWeek))
            EnsureNoOverlap(day.Where(p => shifts.ContainsKey(p.ShiftId)).Select(p => Of(p.ShiftId)).ToList());
        foreach (var exception in _exceptions)
            EnsureNoOverlap(exception.Shifts.Where(s => shifts.ContainsKey(s.ShiftId)).Select(s => Of(s.ShiftId)).ToList());
    }

    /// <summary>Every shift used by the pattern or an exception.</summary>
    public IEnumerable<Guid> ShiftIds() => _patterns.Select(p => p.ShiftId).Concat(_exceptions.SelectMany(e => e.Shifts.Select(s => s.ShiftId))).Distinct();

    /// <summary>The data the working-time calculator needs, given the shifts it refers to.</summary>
    public CalendarSnapshot ToSnapshot(IEnumerable<ShiftInfo> shifts)
        => new(shifts,
            _patterns.GroupBy(p => p.DayOfWeek).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(p => p.ShiftId).ToList()),
            _exceptions.ToDictionary(e => e.Date, e => (e.Type, (IReadOnlyList<Guid>)e.Shifts.Select(s => s.ShiftId).ToList())));

    private static void EnsureNoOverlap(IReadOnlyCollection<ShiftInfo> shifts)
    {
        var list = shifts.ToList();
        for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
                if (list[i].Id == list[j].Id || ShiftMath.Overlap(list[i], list[j]))
                    throw MfgRules.Fail(MfgErrors.Cal002, ("shift", list[i].Code), ("other", list[j].Code));
    }
}
