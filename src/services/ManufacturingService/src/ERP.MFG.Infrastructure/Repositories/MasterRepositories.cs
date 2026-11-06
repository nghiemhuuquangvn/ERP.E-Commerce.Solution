using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using ERP.Shared.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Repositories;

public sealed class PlantCatalog(MfgDbContext db) : IPlantCatalog
{
    public Task<T?> GetAsync<T>(Guid id, CancellationToken ct = default) where T : BaseEntity, IPlantCoded
        => db.Set<T>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    public Task<List<T>> ListAsync<T>(Guid plantId, CancellationToken ct = default) where T : BaseEntity, IPlantCoded
        => db.Set<T>().Where(x => x.PlantId == plantId && !x.IsDeleted).ToListAsync(ct);

    public Task<List<T>> GetManyAsync<T>(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) where T : BaseEntity, IPlantCoded
        => db.Set<T>().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);

    public Task<bool> CodeExistsAsync<T>(Guid plantId, string normalizedCode, CancellationToken ct = default) where T : BaseEntity, IPlantCoded
        => db.Set<T>().AnyAsync(x => x.PlantId == plantId && x.Code == normalizedCode && !x.IsDeleted, ct);

    public void Add<T>(T entity) where T : BaseEntity, IPlantCoded => db.Set<T>().Add(entity);
}

public sealed class CalendarRepository(MfgDbContext db) : ICalendarRepository
{
    private IQueryable<WorkCalendar> Full => db.Calendars.Include(c => c.Patterns).Include(c => c.Exceptions).ThenInclude(e => e.Shifts).Where(c => !c.IsDeleted);

    public Task<WorkCalendar?> GetAsync(Guid id, CancellationToken ct = default) => Full.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<WorkCalendar>> ListAsync(Guid plantId, CancellationToken ct = default) => Full.Where(c => c.PlantId == plantId).ToListAsync(ct);

    public Task<List<WorkCalendar>> UsingShiftAsync(Guid shiftId, CancellationToken ct = default)
        => Full.Where(c => c.Patterns.Any(p => p.ShiftId == shiftId) || c.Exceptions.Any(e => e.Shifts.Any(s => s.ShiftId == shiftId))).ToListAsync(ct);
}

public sealed class CrewRepository(MfgDbContext db) : ICrewRepository
{
    private IQueryable<Crew> Full => db.Crews.Include(c => c.Members).Where(c => !c.IsDeleted);

    public Task<Crew?> GetAsync(Guid id, CancellationToken ct = default) => Full.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<Crew>> ListAsync(Guid plantId, CancellationToken ct = default) => Full.Where(c => c.PlantId == plantId).ToListAsync(ct);

    public async Task<bool> HasOverlappingMembershipAsync(Guid employeeId, DateOnly from, DateOnly? to, Guid? exceptMemberId, CancellationToken ct = default)
    {
        var candidates = await db.CrewMembers.AsNoTracking()
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted && (exceptMemberId == null || m.Id != exceptMemberId))
            .Select(m => new { m.ValidFrom, m.ValidTo }).ToListAsync(ct);
        return candidates.Any(m => CrewMember.Overlaps(from, to, m.ValidFrom, m.ValidTo));
    }

    public async Task<decimal?> OwnRateOnAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
        => await db.CrewMembers.AsNoTracking()
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted && m.LaborRatePerHour != null && m.ValidFrom <= date && (m.ValidTo == null || m.ValidTo >= date))
            .Select(m => m.LaborRatePerHour).FirstOrDefaultAsync(ct);
}

public sealed class ReasonCodeRepository(MfgDbContext db) : IReasonCodeRepository
{
    public Task<ReasonCode?> GetAsync(Guid id, CancellationToken ct = default) => db.ReasonCodes.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public Task<List<ReasonCode>> ListAsync(ReasonType? type, CancellationToken ct = default)
        => db.ReasonCodes.Where(r => !r.IsDeleted && (type == null || r.Type == type)).ToListAsync(ct);

    public async Task<bool> ExistsAsync(ReasonType type, string normalizedCode, CancellationToken ct = default)
        => await db.ReasonCodes.AnyAsync(r => r.Type == type && r.Code == normalizedCode && !r.IsDeleted, ct)
           || db.ChangeTracker.Entries<ReasonCode>().Any(e => e.State == EntityState.Added && e.Entity.Type == type && e.Entity.Code == normalizedCode); // added in this request, not saved yet

    public void Add(ReasonCode reason) => db.ReasonCodes.Add(reason);
}

/// <summary>BR-GEN-02 reference checks over the M2 tables. Routing, BOM and order references join here in their phases.</summary>
public sealed class ReferenceChecker(MfgDbContext db) : IReferenceChecker
{
    public async Task<bool> ShiftIsUsedAsync(Guid shiftId, CancellationToken ct = default)
        => await db.CalendarPatterns.AnyAsync(p => p.ShiftId == shiftId, ct)
           || await db.CalendarExceptionShifts.AnyAsync(s => s.ShiftId == shiftId, ct);

    public async Task<bool> CalendarIsUsedAsync(Guid calendarId, CancellationToken ct = default)
        => await db.WorkCenters.AnyAsync(w => w.CalendarId == calendarId && !w.IsDeleted, ct)
           || await db.Plants.AnyAsync(p => p.DefaultCalendarId == calendarId && !p.IsDeleted, ct);

    public async Task<bool> WorkCenterIsUsedAsync(Guid workCenterId, CancellationToken ct = default)
        => await db.Machines.AnyAsync(m => m.WorkCenterId == workCenterId && !m.IsDeleted, ct)
           || await db.StandardOperations.AnyAsync(o => o.DefaultWorkCenterId == workCenterId && !o.IsDeleted, ct)
           || await db.Crews.AnyAsync(c => c.WorkCenterId == workCenterId && !c.IsDeleted, ct);

    public Task<bool> MachineIsUsedAsync(Guid machineId, CancellationToken ct = default) => Task.FromResult(false);      // downtime / operation reports: Phase 3-4
    public Task<bool> StandardOperationIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);    // copied into routings, never linked (BR-OPS-01)
    public Task<bool> ReasonIsUsedAsync(Guid reasonId, CancellationToken ct = default) => Task.FromResult(false);         // order history, reports: Phase 3-4
    public Task<bool> CrewIsUsedAsync(Guid crewId, CancellationToken ct = default) => Task.FromResult(false);
}
