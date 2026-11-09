using ERP.MFG.Domain.Common;

namespace ERP.MFG.Domain.Scheduling;

/// <summary>A shift as the calculator sees it (BA spec 01 §5.4). The work of a shift runs from its start for <see cref="WorkMinutes"/> minutes - the break is taken at the end (convention of this implementation, R162).</summary>
public sealed record ShiftInfo(Guid Id, string Code, TimeOnly Start, TimeOnly End, int BreakMinutes)
{
    /// <summary>End minus start in minutes, +24 h when the end is not after the start (overnight shift).</summary>
    public int RawMinutes => ShiftMath.RawMinutes(Start, End);

    /// <summary>BR-CAL-01: the raw duration minus the break.</summary>
    public int WorkMinutes => RawMinutes - BreakMinutes;
}

public static class ShiftMath
{
    public static int RawMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (int)(end - start).TotalMinutes; // TimeOnly subtraction wraps over midnight
        return minutes <= 0 ? minutes + 1440 : minutes;
    }

    /// <summary>BR-CAL-01 as a refusal: work minutes in 1..1440 and the break shorter than the raw duration (<c>MFG_CAL_001</c>).</summary>
    public static void EnsureValid(TimeOnly start, TimeOnly end, int breakMinutes)
    {
        var raw = RawMinutes(start, end);
        if (breakMinutes < 0 || breakMinutes >= raw || raw - breakMinutes is < 1 or > 1440)
            throw MfgRules.Fail(MfgErrors.Cal001);
    }

    /// <summary>Do two shifts overlap in time, an overnight shift counting into the next day (touching shifts do not overlap)?</summary>
    public static bool Overlap(ShiftInfo a, ShiftInfo b)
    {
        var aStart = a.Start.Hour * 60 + a.Start.Minute;
        var bStart = b.Start.Hour * 60 + b.Start.Minute;
        var aEnd = aStart + a.RawMinutes;
        var bEnd = bStart + b.RawMinutes;
        foreach (var shift in new[] { -1440, 0, 1440 }) // the other shift of the day before / the same day / the day after
            if (aStart < bEnd + shift && bStart + shift < aEnd) return true;
        return false;
    }
}

/// <summary>A point in working time: a date and the number of working minutes of that day already passed (0 = the start of work, the day's total = the end of work).</summary>
public readonly record struct WorkPoint(DateOnly Date, int WorkingMinute);

/// <summary>The data of one calendar: its shifts, the weekly pattern and the exceptions (BR-CAL-03).</summary>
public sealed class CalendarSnapshot
{
    public CalendarSnapshot(
        IEnumerable<ShiftInfo> shifts,
        IReadOnlyDictionary<DayOfWeek, IReadOnlyList<Guid>> pattern,
        IReadOnlyDictionary<DateOnly, (CalendarExceptionType Type, IReadOnlyList<Guid> ShiftIds)> exceptions)
    {
        Shifts = shifts.ToDictionary(s => s.Id);
        Pattern = pattern;
        Exceptions = exceptions;
    }

    public IReadOnlyDictionary<Guid, ShiftInfo> Shifts { get; }
    public IReadOnlyDictionary<DayOfWeek, IReadOnlyList<Guid>> Pattern { get; }
    public IReadOnlyDictionary<DateOnly, (CalendarExceptionType Type, IReadOnlyList<Guid> ShiftIds)> Exceptions { get; }
}

/// <summary>
/// The working-time arithmetic of BA spec 01 BR-CAL-03 / BR-CAL-05, without any I/O. A day's working minutes are the minutes of its shifts
/// (the weekly pattern, or the exception of that date: a Holiday has none, an extra day / modified day uses the exception's shifts), consumed in shift order.
/// Days with 0 minutes are skipped. A search never looks further than <see cref="HorizonDays"/> days: a calendar with no working day gives a refusal, not an endless loop.
/// </summary>
public sealed class WorkingTimeCalculator(CalendarSnapshot calendar)
{
    public const int HorizonDays = 366;

    private IEnumerable<ShiftInfo> ShiftsOf(DateOnly date)
    {
        IReadOnlyList<Guid>? ids;
        if (calendar.Exceptions.TryGetValue(date, out var exception))
            ids = exception.Type == CalendarExceptionType.Holiday ? [] : exception.ShiftIds;
        else
            ids = calendar.Pattern.TryGetValue(date.DayOfWeek, out var pattern) ? pattern : [];

        return ids.Where(calendar.Shifts.ContainsKey).Select(id => calendar.Shifts[id]).OrderBy(s => s.Start);
    }

    /// <summary>BR-CAL-03: the working minutes of a date.</summary>
    public int GetWorkingMinutes(DateOnly date) => ShiftsOf(date).Sum(s => s.WorkMinutes);

    /// <summary>The working intervals of a date in clock time (an overnight shift ends on the next day).</summary>
    public IReadOnlyList<(DateTime Start, DateTime End)> GetWorkingIntervals(DateOnly date)
        => ShiftsOf(date)
            .Select(s =>
            {
                var start = date.ToDateTime(s.Start);
                return (start, start.AddMinutes(s.WorkMinutes));
            })
            .ToList();

    /// <summary>The clock time of a point in working time (the end of a day's work maps to the end of its last shift).</summary>
    public DateTime ToDateTime(WorkPoint point)
    {
        var remaining = point.WorkingMinute;
        DateTime? last = null;
        foreach (var (start, end) in GetWorkingIntervals(point.Date))
        {
            var length = (int)(end - start).TotalMinutes;
            if (remaining <= length) return start.AddMinutes(remaining);
            remaining -= length;
            last = end;
        }
        return last ?? point.Date.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>
    /// The latest point of working time at or before a clock time (used to hand a schedule from one calendar to another): the working minutes of its
    /// day that lie before it, or the end of the previous working day when the day has not started yet.
    /// </summary>
    public WorkPoint FromDateTime(DateTime time)
    {
        var day = DateOnly.FromDateTime(time);
        for (var i = 0; i <= HorizonDays; i++, day = day.AddDays(-1))
        {
            var intervals = GetWorkingIntervals(day);
            if (intervals.Count == 0 || intervals[0].Start > time) continue;
            var minutes = 0;
            foreach (var (start, end) in intervals)
            {
                if (time >= end) minutes += (int)(end - start).TotalMinutes;
                else if (time > start) { minutes += (int)(time - start).TotalMinutes; break; }
                else break;
            }
            return new WorkPoint(day, minutes);
        }
        throw NoWorkingTime();
    }

    /// <summary>
    /// The earliest point of working time at or after a clock time (the start of a forward schedule, or the hand-over to the next calendar):
    /// inside a shift the minutes already worked that day, otherwise the start of the next shift. A time at the very end of a day's work moves to the next working day.
    /// </summary>
    public WorkPoint FromDateTimeForward(DateTime time)
    {
        var day = DateOnly.FromDateTime(time);
        for (var i = 0; i <= HorizonDays; i++, day = day.AddDays(1))
        {
            var minutes = 0;
            foreach (var (start, end) in GetWorkingIntervals(day))
            {
                if (time >= end) { minutes += (int)(end - start).TotalMinutes; continue; }
                return new WorkPoint(day, time > start ? minutes + (int)(time - start).TotalMinutes : minutes);
            }
        }
        throw NoWorkingTime();
    }

    /// <summary>Moves forward by working minutes. A result that lands exactly on the end of a day's work stays on that day.</summary>
    public WorkPoint AddWorkingMinutes(WorkPoint from, int minutes)
    {
        if (minutes < 0) return SubtractWorkingMinutes(from, -minutes);
        var day = from.Date;
        var position = from.WorkingMinute;
        for (var i = 0; i <= HorizonDays; i++)
        {
            var available = GetWorkingMinutes(day) - position;
            if (minutes <= available && (available > 0 || minutes == 0)) return new WorkPoint(day, position + minutes);
            minutes -= Math.Max(available, 0);
            day = day.AddDays(1);
            position = 0;
        }
        throw NoWorkingTime();
    }

    /// <summary>Moves backward by working minutes (TC-CAL-010: end of Monday 16/11 minus 1000 = Saturday 14/11, working minute 350).</summary>
    public WorkPoint SubtractWorkingMinutes(WorkPoint to, int minutes)
    {
        if (minutes < 0) return AddWorkingMinutes(to, -minutes);
        var day = to.Date;
        var position = to.WorkingMinute;
        for (var i = 0; i <= HorizonDays; i++)
        {
            if (minutes <= position && (position > 0 || minutes == 0)) return new WorkPoint(day, position - minutes);
            minutes -= Math.Max(position, 0);
            day = day.AddDays(-1);
            position = GetWorkingMinutes(day);
        }
        throw NoWorkingTime();
    }

    /// <summary>The date <paramref name="days"/> working days after <paramref name="from"/> (days without working minutes do not count).</summary>
    public DateOnly AddWorkingDays(DateOnly from, int days)
    {
        if (days < 0) return SubtractWorkingDays(from, -days);
        var day = from;
        var left = days;
        for (var i = 0; i <= HorizonDays + days && left > 0; i++)
        {
            day = day.AddDays(1);
            if (GetWorkingMinutes(day) > 0) left--;
        }
        return left == 0 ? day : throw NoWorkingTime();
    }

    /// <summary>The date <paramref name="days"/> working days before <paramref name="from"/>.</summary>
    public DateOnly SubtractWorkingDays(DateOnly from, int days)
    {
        if (days < 0) return AddWorkingDays(from, -days);
        var day = from;
        var left = days;
        for (var i = 0; i <= HorizonDays + days && left > 0; i++)
        {
            day = day.AddDays(-1);
            if (GetWorkingMinutes(day) > 0) left--;
        }
        return left == 0 ? day : throw NoWorkingTime();
    }

    private static Exception NoWorkingTime() => MfgRules.Invalid($"The calendar has no working time within {HorizonDays} days of the requested date.");
}
