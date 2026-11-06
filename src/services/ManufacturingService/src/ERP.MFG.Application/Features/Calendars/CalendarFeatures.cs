using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Calendars;

/// <summary>Loads a plant master by id and makes sure the caller may see its plant (otherwise it does not exist for them: 404, BR-GEN-09).</summary>
internal static class PlantScope
{
    public static async Task<T> LoadAsync<T>(IPlantCatalog catalog, IPlantAccessService access, Guid id, CancellationToken ct) where T : BaseEntity, IPlantCoded
    {
        var entity = await catalog.GetAsync<T>(id, ct) ?? throw new EntityNotFoundException(typeof(T).Name, id);
        await access.GetVisiblePlantAsync(entity.PlantId, ct);
        return entity;
    }

    /// <summary>The code must not exist yet in the plant (any case): <c>MFG_GEN_006</c>.</summary>
    public static async Task EnsureCodeFreeAsync<T>(IPlantCatalog catalog, Guid plantId, string code, CancellationToken ct) where T : BaseEntity, IPlantCoded
    {
        var normal = MasterCode.Normalize(code);
        if (await catalog.CodeExistsAsync<T>(plantId, normal, ct)) throw MfgRules.Fail(MfgErrors.Gen006, ("code", normal));
    }
}

// ── Shifts (A04) ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record ShiftDto(Guid Id, Guid PlantId, string Code, string Name, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes, int DurationMinutes, bool IsActive)
{
    public static ShiftDto From(Shift s) => new(s.Id, s.PlantId, s.Code, s.Name, s.StartTime, s.EndTime, s.BreakMinutes, s.DurationMinutes, s.IsActive);
}

public sealed record CreateShiftCommand(Guid PlantId, string Code, string Name, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes) : IRequest<ShiftDto>;

public sealed class CreateShiftHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<CreateShiftCommand, ShiftDto>
{
    public async Task<ShiftDto> Handle(CreateShiftCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var shift = Shift.Create(plant.Id, r.Code, r.Name, r.StartTime, r.EndTime, r.BreakMinutes);
        await PlantScope.EnsureCodeFreeAsync<Shift>(catalog, plant.Id, shift.Code, ct);
        catalog.Add(shift);
        await uow.SaveChangesAsync(ct);
        return ShiftDto.From(shift);
    }
}

public sealed record UpdateShiftCommand(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes) : IRequest<ShiftDto>;

public sealed class UpdateShiftHandler(IPlantAccessService access, IPlantCatalog catalog, ICalendarRepository calendars, IWorkingTimeService workingTime, IMfgUnitOfWork uow)
    : IRequestHandler<UpdateShiftCommand, ShiftDto>
{
    public async Task<ShiftDto> Handle(UpdateShiftCommand r, CancellationToken ct)
    {
        var shift = await PlantScope.LoadAsync<Shift>(catalog, access, r.Id, ct);
        shift.Update(r.Name, r.StartTime, r.EndTime, r.BreakMinutes);

        // The new times must not make the shifts of any calendar that uses this one overlap (BR-CAL-02).
        var used = await calendars.UsingShiftAsync(shift.Id, ct);
        foreach (var calendar in used)
        {
            var infos = (await catalog.GetManyAsync<Shift>(calendar.ShiftIds().ToList(), ct)).ToDictionary(s => s.Id, s => s.ToInfo());
            infos[shift.Id] = shift.ToInfo();
            calendar.Revalidate(infos);
        }

        await uow.SaveChangesAsync(ct);
        foreach (var calendar in used) workingTime.Invalidate(calendar.Id);
        return ShiftDto.From(shift);
    }
}

public sealed record SetShiftActiveCommand(Guid Id, bool Active) : IRequest<ShiftDto>;

public sealed class SetShiftActiveHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<SetShiftActiveCommand, ShiftDto>
{
    public async Task<ShiftDto> Handle(SetShiftActiveCommand r, CancellationToken ct)
    {
        var shift = await PlantScope.LoadAsync<Shift>(catalog, access, r.Id, ct);
        shift.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return ShiftDto.From(shift);
    }
}

public sealed record DeleteShiftCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteShiftHandler(IPlantAccessService access, IPlantCatalog catalog, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteShiftCommand, Unit>
{
    public async Task<Unit> Handle(DeleteShiftCommand r, CancellationToken ct)
    {
        var shift = await PlantScope.LoadAsync<Shift>(catalog, access, r.Id, ct);
        if (await references.ShiftIsUsedAsync(shift.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        shift.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record GetShiftsQuery(Guid PlantId) : IRequest<List<ShiftDto>>;

public sealed class GetShiftsHandler(IPlantAccessService access, IPlantCatalog catalog) : IRequestHandler<GetShiftsQuery, List<ShiftDto>>
{
    public async Task<List<ShiftDto>> Handle(GetShiftsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await catalog.ListAsync<Shift>(plant.Id, ct)).OrderBy(s => s.StartTime).ThenBy(s => s.Code).Select(ShiftDto.From).ToList();
    }
}

// ── Calendars (A05) ──────────────────────────────────────────────────────────────────────────────────────────

public sealed record CalendarDayDto(DayOfWeek DayOfWeek, List<Guid> ShiftIds, int WorkingMinutes);
public sealed record CalendarExceptionDto(DateOnly Date, CalendarExceptionType Type, string? Description, List<Guid> ShiftIds, int WorkingMinutes);
public sealed record CalendarDto(Guid Id, Guid PlantId, string Code, string Name, bool IsActive, List<CalendarDayDto> Week, List<CalendarExceptionDto> Exceptions)
{
    public static async Task<CalendarDto> BuildAsync(WorkCalendar c, IPlantCatalog catalog, CancellationToken ct)
    {
        var shifts = (await catalog.GetManyAsync<Shift>(c.ShiftIds().ToList(), ct)).ToDictionary(s => s.Id, s => s.ToInfo());
        var calculator = new WorkingTimeCalculator(c.ToSnapshot(shifts.Values));
        var week = Enum.GetValues<DayOfWeek>().OrderBy(d => ((int)d + 6) % 7) // Monday first
            .Select(d => new CalendarDayDto(d, c.Patterns.Where(p => p.DayOfWeek == d).Select(p => p.ShiftId).ToList(),
                c.Patterns.Where(p => p.DayOfWeek == d && shifts.ContainsKey(p.ShiftId)).Sum(p => shifts[p.ShiftId].WorkMinutes)))
            .ToList();
        var exceptions = c.Exceptions.OrderBy(e => e.Date)
            .Select(e => new CalendarExceptionDto(e.Date, e.Type, e.Description, e.Shifts.Select(s => s.ShiftId).ToList(), calculator.GetWorkingMinutes(e.Date)))
            .ToList();
        return new CalendarDto(c.Id, c.PlantId, c.Code, c.Name, c.IsActive, week, exceptions);
    }
}

public sealed record CreateCalendarCommand(Guid PlantId, string Code, string Name) : IRequest<CalendarDto>;

public sealed class CreateCalendarHandler(IPlantAccessService access, IPlantCatalog catalog, ICalendarRepository calendars, IMfgUnitOfWork uow) : IRequestHandler<CreateCalendarCommand, CalendarDto>
{
    public async Task<CalendarDto> Handle(CreateCalendarCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var calendar = WorkCalendar.Create(plant.Id, r.Code, r.Name);
        await PlantScope.EnsureCodeFreeAsync<WorkCalendar>(catalog, plant.Id, calendar.Code, ct);
        catalog.Add(calendar);
        await uow.SaveChangesAsync(ct);
        return await CalendarDto.BuildAsync(calendar, catalog, ct);
    }
}

public sealed record RenameCalendarCommand(Guid Id, string Name) : IRequest<CalendarDto>;

public sealed class RenameCalendarHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<RenameCalendarCommand, CalendarDto>
{
    public async Task<CalendarDto> Handle(RenameCalendarCommand r, CancellationToken ct)
    {
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.Id, ct);
        calendar.Rename(r.Name);
        await uow.SaveChangesAsync(ct);
        return await CalendarDto.BuildAsync(calendar, catalog, ct);
    }
}

internal static class CalendarAccess
{
    public static async Task<WorkCalendar> LoadAsync(ICalendarRepository calendars, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var calendar = await calendars.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(WorkCalendar), id);
        await access.GetVisiblePlantAsync(calendar.PlantId, ct);
        return calendar;
    }

    /// <summary>The shifts must exist, belong to the calendar's plant and be active.</summary>
    public static async Task<List<ShiftInfo>> ShiftsAsync(IPlantCatalog catalog, WorkCalendar calendar, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var shifts = await catalog.GetManyAsync<Shift>(distinct, ct);
        foreach (var id in distinct)
        {
            var shift = shifts.FirstOrDefault(s => s.Id == id);
            if (shift is null || shift.PlantId != calendar.PlantId) throw MfgRules.Invalid($"Shift {id} does not exist in this plant.");
            if (!shift.IsActive) throw MfgRules.Invalid($"Shift {shift.Code} is not active.");
        }
        return shifts.Select(s => s.ToInfo()).ToList();
    }
}

public sealed record SetCalendarDayCommand(Guid CalendarId, DayOfWeek DayOfWeek, List<Guid> ShiftIds) : IRequest<CalendarDto>;

public sealed class SetCalendarDayHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog, IWorkingTimeService workingTime, IMfgUnitOfWork uow)
    : IRequestHandler<SetCalendarDayCommand, CalendarDto>
{
    public async Task<CalendarDto> Handle(SetCalendarDayCommand r, CancellationToken ct)
    {
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.CalendarId, ct);
        var shifts = await CalendarAccess.ShiftsAsync(catalog, calendar, r.ShiftIds, ct);
        calendar.SetDayPattern(r.DayOfWeek, shifts);
        await uow.SaveChangesAsync(ct);
        workingTime.Invalidate(calendar.Id);
        return await CalendarDto.BuildAsync(calendar, catalog, ct);
    }
}

public sealed record AddCalendarExceptionCommand(Guid CalendarId, DateOnly Date, CalendarExceptionType Type, string? Description, List<Guid>? ShiftIds) : IRequest<CalendarDto>;

public sealed class AddCalendarExceptionHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog, IWorkingTimeService workingTime, IMfgUnitOfWork uow)
    : IRequestHandler<AddCalendarExceptionCommand, CalendarDto>
{
    public async Task<CalendarDto> Handle(AddCalendarExceptionCommand r, CancellationToken ct)
    {
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.CalendarId, ct);
        var shifts = await CalendarAccess.ShiftsAsync(catalog, calendar, r.ShiftIds ?? [], ct);
        calendar.AddException(r.Date, r.Type, r.Description, shifts);
        await uow.SaveChangesAsync(ct);
        workingTime.Invalidate(calendar.Id);
        return await CalendarDto.BuildAsync(calendar, catalog, ct);
    }
}

public sealed record RemoveCalendarExceptionCommand(Guid CalendarId, DateOnly Date) : IRequest<CalendarDto>;

public sealed class RemoveCalendarExceptionHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog, IWorkingTimeService workingTime, IMfgUnitOfWork uow)
    : IRequestHandler<RemoveCalendarExceptionCommand, CalendarDto>
{
    public async Task<CalendarDto> Handle(RemoveCalendarExceptionCommand r, CancellationToken ct)
    {
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.CalendarId, ct);
        if (calendar.RemoveException(r.Date) is null) throw new EntityNotFoundException("CalendarException", r.CalendarId);
        await uow.SaveChangesAsync(ct);
        workingTime.Invalidate(calendar.Id);
        return await CalendarDto.BuildAsync(calendar, catalog, ct);
    }
}

public sealed record SetCalendarActiveCommand(Guid Id, bool Active) : IRequest<CalendarDto>;

public sealed class SetCalendarActiveHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<SetCalendarActiveCommand, CalendarDto>
{
    public async Task<CalendarDto> Handle(SetCalendarActiveCommand r, CancellationToken ct)
    {
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.Id, ct);
        calendar.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return await CalendarDto.BuildAsync(calendar, catalog, ct);
    }
}

public sealed record DeleteCalendarCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteCalendarHandler(IPlantAccessService access, ICalendarRepository calendars, IReferenceChecker references, IWorkingTimeService workingTime, IMfgUnitOfWork uow) : IRequestHandler<DeleteCalendarCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCalendarCommand r, CancellationToken ct)
    {
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.Id, ct);
        if (await references.CalendarIsUsedAsync(calendar.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        calendar.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        workingTime.Invalidate(calendar.Id);
        return Unit.Value;
    }
}

public sealed record GetCalendarsQuery(Guid PlantId) : IRequest<List<CalendarDto>>;

public sealed class GetCalendarsHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog) : IRequestHandler<GetCalendarsQuery, List<CalendarDto>>
{
    public async Task<List<CalendarDto>> Handle(GetCalendarsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var result = new List<CalendarDto>();
        foreach (var calendar in (await calendars.ListAsync(plant.Id, ct)).OrderBy(c => c.Code))
            result.Add(await CalendarDto.BuildAsync(calendar, catalog, ct));
        return result;
    }
}

public sealed record GetCalendarQuery(Guid Id) : IRequest<CalendarDto>;

public sealed class GetCalendarHandler(IPlantAccessService access, ICalendarRepository calendars, IPlantCatalog catalog) : IRequestHandler<GetCalendarQuery, CalendarDto>
{
    public async Task<CalendarDto> Handle(GetCalendarQuery r, CancellationToken ct)
        => await CalendarDto.BuildAsync(await CalendarAccess.LoadAsync(calendars, access, r.Id, ct), catalog, ct);
}

// ── Month view and working-time questions (BR-CAL-03, BR-CAL-05) ─────────────────────────────────────────────

public sealed record MonthDayDto(DateOnly Date, int WorkingMinutes, bool IsException, CalendarExceptionType? ExceptionType);

public sealed record GetCalendarMonthQuery(Guid CalendarId, int Year, int Month) : IRequest<List<MonthDayDto>>;

public sealed class GetCalendarMonthHandler(IPlantAccessService access, ICalendarRepository calendars, IWorkingTimeService workingTime) : IRequestHandler<GetCalendarMonthQuery, List<MonthDayDto>>
{
    public async Task<List<MonthDayDto>> Handle(GetCalendarMonthQuery r, CancellationToken ct)
    {
        if (r.Year is < 2000 or > 2100 || r.Month is < 1 or > 12) throw MfgRules.Invalid("Choose a month between 2000 and 2100.");
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.CalendarId, ct);
        var calculator = await workingTime.ForCalendarAsync(calendar.Id, ct);
        var days = DateTime.DaysInMonth(r.Year, r.Month);
        return Enumerable.Range(1, days).Select(d =>
        {
            var date = new DateOnly(r.Year, r.Month, d);
            var exception = calendar.Exceptions.FirstOrDefault(e => e.Date == date);
            return new MonthDayDto(date, calculator.GetWorkingMinutes(date), exception is not null, exception?.Type);
        }).ToList();
    }
}

public enum WorkingTimeOperation { AddMinutes, SubtractMinutes, AddDays, SubtractDays }

public sealed record WorkingTimeResult(DateOnly Date, int WorkingMinute, DateTime? ClockTime);

/// <summary>Asks the calculator (also used by the tests of the spec and by planners to check a calendar): add / subtract working minutes from a point, or working days from a date.</summary>
public sealed record WorkingTimeQuery(Guid CalendarId, WorkingTimeOperation Operation, DateOnly Date, int WorkingMinute, int Amount) : IRequest<WorkingTimeResult>;

public sealed class WorkingTimeHandler(IPlantAccessService access, ICalendarRepository calendars, IWorkingTimeService workingTime) : IRequestHandler<WorkingTimeQuery, WorkingTimeResult>
{
    public async Task<WorkingTimeResult> Handle(WorkingTimeQuery r, CancellationToken ct)
    {
        if (r.Amount < 0 || r.Amount > 1_000_000) throw MfgRules.Invalid("The amount must be between 0 and 1,000,000.");
        var calendar = await CalendarAccess.LoadAsync(calendars, access, r.CalendarId, ct);
        var calculator = await workingTime.ForCalendarAsync(calendar.Id, ct);

        switch (r.Operation)
        {
            case WorkingTimeOperation.AddDays:
                return new WorkingTimeResult(calculator.AddWorkingDays(r.Date, r.Amount), 0, null);
            case WorkingTimeOperation.SubtractDays:
                return new WorkingTimeResult(calculator.SubtractWorkingDays(r.Date, r.Amount), 0, null);
        }

        var from = new WorkPoint(r.Date, r.WorkingMinute);
        var point = r.Operation == WorkingTimeOperation.AddMinutes ? calculator.AddWorkingMinutes(from, r.Amount) : calculator.SubtractWorkingMinutes(from, r.Amount);
        return new WorkingTimeResult(point.Date, point.WorkingMinute, calculator.ToDateTime(point));
    }
}
