using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

/// <summary>BR-WC-02: capacity of a work center per day = working minutes of its calendar x resources x efficiency% x utilization%.</summary>
public sealed class CapacityService(IPlantCatalog catalog, IWorkingTimeService workingTime) : ICapacityService
{
    public const int MaxDays = 400;

    public async Task<IReadOnlyList<DayCapacity>> GetDailyCapacityAsync(Guid workCenterId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) throw MfgRules.Invalid("The end date is before the start date.");
        if (to.DayNumber - from.DayNumber + 1 > MaxDays) throw MfgRules.Invalid($"Ask for at most {MaxDays} days at a time.");

        var center = await catalog.GetAsync<WorkCenter>(workCenterId, ct) ?? throw new EntityNotFoundException(nameof(WorkCenter), workCenterId);
        var calculator = await workingTime.ForCalendarAsync(center.CalendarId, ct);
        var result = new List<DayCapacity>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var minutes = calculator.GetWorkingMinutes(day);
            result.Add(new DayCapacity(day, minutes, center.DailyCapacity(minutes)));
        }
        return result;
    }
}

/// <summary>BR-LBR-02: an employee's own rate (crew membership valid on the date) beats the work center's labor rate.</summary>
public sealed class LaborRateResolver(ICrewRepository crews, IPlantCatalog catalog) : ILaborRateResolver
{
    public async Task<decimal> GetRateAsync(Guid employeeId, Guid workCenterId, DateOnly date, CancellationToken ct = default)
    {
        if (await crews.OwnRateOnAsync(employeeId, date, ct) is { } own) return own;
        var center = await catalog.GetAsync<WorkCenter>(workCenterId, ct) ?? throw new EntityNotFoundException(nameof(WorkCenter), workCenterId);
        return center.LaborRatePerHour;
    }
}
