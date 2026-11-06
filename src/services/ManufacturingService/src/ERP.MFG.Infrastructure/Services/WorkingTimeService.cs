using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.MFG.Infrastructure.Services;

/// <summary>
/// Gives the working-time calculator of a calendar. The calendar and its shifts are read once and kept 60 s per tenant (key carries the tenant);
/// a change to a calendar or one of its shifts drops the entry on this instance (another replica may use the old calendar for up to 60 s, R164).
/// </summary>
public sealed class WorkingTimeService(ICalendarRepository calendars, IPlantCatalog catalog, IMemoryCache cache, ITenantProvider tenant) : IWorkingTimeService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private string? Key(Guid calendarId) => tenant.CurrentTenantId is { } t ? $"mfg:calendar:t:{t}:{calendarId}" : null;

    public async Task<WorkingTimeCalculator> ForCalendarAsync(Guid calendarId, CancellationToken ct = default)
    {
        var key = Key(calendarId);
        if (key is not null && cache.TryGetValue(key, out WorkingTimeCalculator? hit) && hit is not null) return hit;

        var calendar = await calendars.GetAsync(calendarId, ct) ?? throw new EntityNotFoundException(nameof(WorkCalendar), calendarId);
        var shifts = await catalog.GetManyAsync<Shift>(calendar.ShiftIds().ToList(), ct);
        var calculator = new WorkingTimeCalculator(calendar.ToSnapshot(shifts.Select(s => s.ToInfo())));
        if (key is not null) cache.Set(key, calculator, Ttl);
        return calculator;
    }

    public void Invalidate(Guid calendarId)
    {
        if (Key(calendarId) is { } key) cache.Remove(key);
    }
}
