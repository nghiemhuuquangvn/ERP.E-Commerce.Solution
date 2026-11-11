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
           || await db.Crews.AnyAsync(c => c.WorkCenterId == workCenterId && !c.IsDeleted, ct)
           || await db.RoutingOperations.AnyAsync(o => o.WorkCenterId == workCenterId && !o.IsDeleted, ct)  // routings join the check (R165)
           || await db.ProductionOrderOperations.AnyAsync(o => o.WorkCenterId == workCenterId && !o.IsDeleted, ct); // and the operations of orders

    public Task<bool> MachineIsUsedAsync(Guid machineId, CancellationToken ct = default) => Task.FromResult(false);      // downtime / operation reports: Phase 3-4
    public Task<bool> StandardOperationIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);    // copied into routings, never linked (BR-OPS-01)
    public Task<bool> ReasonIsUsedAsync(Guid reasonId, CancellationToken ct = default)                                    // order history (reports: Phase 3 P3-4)
        => db.ProductionOrderStatusHistories.AnyAsync(h => h.ReasonCodeId == reasonId, ct);
    public Task<bool> CrewIsUsedAsync(Guid crewId, CancellationToken ct = default) => Task.FromResult(false);

    public async Task<bool> InspectionPlanIsUsedAsync(Guid planId, CancellationToken ct = default)
        => await db.ItemProfiles.AnyAsync(p => p.FqcInspectionPlanId == planId && !p.IsDeleted, ct)
           || await db.RoutingOperations.AnyAsync(o => o.IpqcInspectionPlanId == planId && !o.IsDeleted, ct)
           || await db.ProductionOrderOperations.AnyAsync(o => o.IpqcInspectionPlanId == planId && !o.IsDeleted, ct);
}

public sealed class InspectionPlanRepository(MfgDbContext db) : IInspectionPlanRepository
{
    private IQueryable<InspectionPlan> Full => db.InspectionPlans.Include(p => p.AllCharacteristics).Where(p => !p.IsDeleted);

    public Task<InspectionPlan?> GetAsync(Guid id, CancellationToken ct = default) => Full.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<InspectionPlan>> ListAsync(Guid plantId, InspectionType? type, Guid? itemId, VersionStatus? status, CancellationToken ct = default)
        => Full.Where(p => p.PlantId == plantId && (type == null || p.Type == type) && (itemId == null || p.ItemId == itemId) && (status == null || p.Status == status)).ToListAsync(ct);

    public Task<List<InspectionPlan>> ListSeriesAsync(Guid plantId, string code, CancellationToken ct = default)
        => Full.Where(p => p.PlantId == plantId && p.Code == code).ToListAsync(ct);

    public async Task<int> MaxVersionAsync(Guid plantId, string code, CancellationToken ct = default)
        => await db.InspectionPlans.Where(p => p.PlantId == plantId && p.Code == code).Select(p => (int?)p.Version).MaxAsync(ct) ?? 0; // deleted drafts keep their number

    public void Add(InspectionPlan plan) => db.InspectionPlans.Add(plan);
}

public sealed class InspectionRepository(MfgDbContext db) : IInspectionRepository
{
    public Task<Inspection?> GetAsync(Guid id, CancellationToken ct = default) => db.Inspections.Include(i => i.AllResults).FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);

    public async Task<(List<Inspection> Items, int Total)> ListAsync(InspectionFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.Inspections.Where(i => !i.IsDeleted && i.PlantId == f.PlantId
            && (f.Type == null || i.Type == f.Type) && (f.Status == null || i.Status == f.Status) && (f.OrderId == null || i.OrderId == f.OrderId) && (f.ItemId == null || i.ItemId == f.ItemId));
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            q = q.Where(i => i.DocNo.ToLower().Contains(term) || i.OrderNo.ToLower().Contains(term) || i.ItemCode.ToLower().Contains(term) || (i.LotNumber != null && i.LotNumber.ToLower().Contains(term)));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(i => i.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<List<Inspection>> ListForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.Inspections.Where(i => !i.IsDeleted && i.OrderId == orderId).OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public Task<List<Inspection>> ListForReceiptLinesAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken ct = default)
        => db.Inspections.Where(i => !i.IsDeleted && i.ReceiptLineId != null && lineIds.Contains(i.ReceiptLineId.Value)).OrderBy(i => i.CreatedAt).ToListAsync(ct);

    public Task<List<Inspection>> ListForReportAsync(Guid reportId, CancellationToken ct = default)
        => db.Inspections.Where(i => !i.IsDeleted && i.OperationReportId == reportId).OrderBy(i => i.CreatedAt).ToListAsync(ct);

    public Task<int> CountOpenAsync(Guid orderId, CancellationToken ct = default)
        => db.Inspections.CountAsync(i => !i.IsDeleted && i.OrderId == orderId && (i.Status == InspectionStatus.Pending || (i.Status == InspectionStatus.Failed && i.ReinspectedById == null)), ct);

    public void Add(Inspection inspection) => db.Inspections.Add(inspection);
}

public sealed class BomRepository(MfgDbContext db) : IBomRepository
{
    private IQueryable<Bom> Full => db.Boms.Include(b => b.AllLines).Where(b => !b.IsDeleted);

    public Task<Bom?> GetAsync(Guid id, CancellationToken ct = default) => Full.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Bom>> ListAsync(Guid plantId, Guid? itemId, VersionStatus? status, CancellationToken ct = default)
        => Full.Where(b => b.PlantId == plantId && (itemId == null || b.ItemId == itemId) && (status == null || b.Status == status)).ToListAsync(ct);

    public Task<List<Bom>> ListApprovedAsync(Guid plantId, CancellationToken ct = default)
        => Full.Where(b => b.PlantId == plantId && b.Status == VersionStatus.Approved).ToListAsync(ct);

    public async Task<int> MaxVersionAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => await db.Boms.Where(b => b.ItemId == itemId && b.PlantId == plantId).Select(b => (int?)b.Version).MaxAsync(ct) ?? 0; // deleted drafts keep their number

    public void Add(Bom bom) => db.Boms.Add(bom);
}

public sealed class RoutingRepository(MfgDbContext db) : IRoutingRepository
{
    private IQueryable<Routing> Full => db.Routings.Include(r => r.AllOperations).Where(r => !r.IsDeleted);

    public Task<Routing?> GetAsync(Guid id, CancellationToken ct = default) => Full.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<Routing>> ListAsync(Guid plantId, Guid? itemId, VersionStatus? status, CancellationToken ct = default)
        => Full.Where(r => r.PlantId == plantId && (itemId == null || r.ItemId == itemId) && (status == null || r.Status == status)).ToListAsync(ct);

    public Task<List<Routing>> ListApprovedAsync(Guid plantId, CancellationToken ct = default)
        => Full.Where(r => r.PlantId == plantId && r.Status == VersionStatus.Approved).ToListAsync(ct);

    public async Task<int> MaxVersionAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => await db.Routings.Where(r => r.ItemId == itemId && r.PlantId == plantId).Select(r => (int?)r.Version).MaxAsync(ct) ?? 0;

    public Task<bool> WorkCenterIsUsedAsync(Guid workCenterId, CancellationToken ct = default)
        => db.RoutingOperations.AnyAsync(o => o.WorkCenterId == workCenterId && !o.IsDeleted, ct);

    public void Add(Routing routing) => db.Routings.Add(routing);
}
