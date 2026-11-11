using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.UnitTests.Boms;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Core.Domain;
using ERP.Shared.Authorization;

namespace ERP.MFG.UnitTests.Common;

/// <summary>In-memory stand-ins for the persistence and client ports: the handlers run for real, only the storage and the other services are replaced.</summary>
public sealed class Fakes
{
    public readonly List<Plant> Plants = [];
    public readonly List<ProductionSetting> Settings = [];
    public readonly List<ProductionSettingAudit> Audits = [];
    public readonly List<ItemManufacturingProfile> Profiles = [];
    public readonly List<PlantUserAccess> Access = [];
    public int Saves;

    // M2 stores: one list per plant-scoped master type, plus calendars / crews / reasons
    private readonly Dictionary<Type, System.Collections.IList> _masters = [];
    public List<T> Of<T>() where T : BaseEntity, IPlantCoded
    {
        if (!_masters.TryGetValue(typeof(T), out var list)) _masters[typeof(T)] = list = new List<T>();
        return (List<T>)list;
    }
    public readonly List<WorkCalendar> Calendars = [];
    public readonly List<Crew> Crews = [];
    public readonly List<ReasonCode> Reasons = [];
    public readonly Dictionary<Guid, EmployeeInfo> Employees = [];
    public bool WorkCenterBusy;
    public HashSet<Guid> UsedShifts = [];
    public HashSet<Guid> UsedCalendars = [];
    public HashSet<Guid> UsedWorkCenters = [];

    public readonly Dictionary<Guid, WarehouseInfo> Warehouses = [];
    public readonly Dictionary<Guid, ProductInfo> Products = [];
    public readonly List<Bom> Boms = [];
    public readonly List<Routing> Routings = [];
    public readonly List<InspectionPlan> InspectionPlans = [];
    public readonly Dictionary<Guid, UnitInfo> Units = [];
    public readonly List<ProductConversionFacts> ProductConversions = [];
    public bool OpenOrders;
    /// <summary>Orders of the plant that are neither Closed nor Cancelled (R219).</summary>
    public int UnfinishedOrders;
    public bool Unreachable;

    public IPlantRepository PlantRepo => new PlantRepo(this);
    public IProductionSettingRepository SettingRepo => new SettingRepo(this);
    public IItemProfileRepository ProfileRepo => new ProfileRepo(this);
    public IPlantAccessRepository AccessRepo => new AccessRepo(this);
    public IMfgUnitOfWork Uow => new Uow(this);
    public IBomRepository BomRepo => new BomFake(this);
    public IRoutingRepository RoutingRepo => new RoutingFake(this);
    public IInspectionPlanRepository PlanRepo => new InspectionPlanFake(this);
    public IUnitOfMeasureClient UnitClient => new UnitFake(this);
    public IProductUomConversionClient ConversionClient => new ConversionFake(this);
    public IWarehouseLookupClient WarehouseClient => new WarehouseClient(this);
    public IProductLookupClient ProductClient => new ProductClient(this);
    public IOpenOrderGuard Guard => new Guard(this);
    public IPlantCatalog Catalog => new CatalogFake(this);
    public ICalendarRepository CalendarRepo => new CalendarFake(this);
    public ICrewRepository CrewRepo => new CrewFake(this);
    public IReasonCodeRepository ReasonRepo => new ReasonFake(this);
    public IReferenceChecker References => new ReferenceFake(this);
    public IEmployeeLookupClient EmployeeClient => new EmployeeFake(this);
    public RecordingWorkingTime WorkingTime { get; } = new();

    public Guid AddEmployee(string number, bool active = true)
    {
        var id = Guid.NewGuid();
        Employees[id] = new EmployeeInfo(id, number, "Employee " + number, active);
        return id;
    }

    public Guid AddWarehouse(string code, bool active = true)
    {
        var id = Guid.NewGuid();
        Warehouses[id] = new WarehouseInfo(id, code, code, active);
        return id;
    }

    public ProductInfo AddProduct(string sku, bool active = true, bool withUom = true)
    {
        var p = new ProductInfo(Guid.NewGuid(), sku, "Product " + sku, active, withUom ? Guid.NewGuid() : null);
        Products[p.Id] = p;
        return p;
    }

    /// <summary>A plant with usable warehouses, its default settings, stored in the fake.</summary>
    public Plant AddPlant(string code = "PL01")
    {
        var plant = Plant.Create(code, "Plant " + code, null, AddWarehouse(code + "-RM"), AddWarehouse(code + "-FG"), AddWarehouse(code + "-QC"), null, null);
        Plants.Add(plant);
        Settings.Add(ProductionSetting.CreateDefault(plant.Id));
        return plant;
    }
}

internal sealed class PlantRepo(Fakes f) : IPlantRepository
{
    public Task<Plant?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Plants.FirstOrDefault(p => p.Id == id));
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default) => Task.FromResult(f.Plants.Any(p => p.Code == code && p.Id != exceptId));
    public Task<List<Plant>> ListAsync(IReadOnlyCollection<Guid>? onlyIds, bool activeOnly, CancellationToken ct = default)
        => Task.FromResult(f.Plants.Where(p => (onlyIds == null || onlyIds.Contains(p.Id)) && (!activeOnly || p.IsActive)).ToList());
    public void Add(Plant plant) => f.Plants.Add(plant);
}

internal sealed class SettingRepo(Fakes f) : IProductionSettingRepository
{
    public Task<ProductionSetting?> GetByPlantAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Settings.FirstOrDefault(s => s.PlantId == plantId));
    public Task<List<ProductionSettingAudit>> GetAuditAsync(Guid plantId, int take, CancellationToken ct = default)
        => Task.FromResult(f.Audits.Where(a => a.PlantId == plantId).OrderByDescending(a => a.ChangedAt).Take(take).ToList());
    public void Add(ProductionSetting setting) => f.Settings.Add(setting);
    public void AddAudit(ProductionSettingAudit audit) => f.Audits.Add(audit);
}

internal sealed class ProfileRepo(Fakes f) : IItemProfileRepository
{
    public Task<ItemManufacturingProfile?> GetAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => Task.FromResult(f.Profiles.FirstOrDefault(p => p.ItemId == itemId && p.PlantId == plantId));
    public Task<(List<ItemManufacturingProfile> Items, int Total)> ListAsync(Guid plantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var all = f.Profiles.Where(p => p.PlantId == plantId && (string.IsNullOrWhiteSpace(search) || p.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase))).OrderBy(p => p.ItemCode).ToList();
        return Task.FromResult((all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }
    public Task<List<ItemManufacturingProfile>> ListByPlantAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Profiles.Where(p => p.PlantId == plantId).ToList());
    public void Add(ItemManufacturingProfile profile) => f.Profiles.Add(profile);
}

internal sealed class AccessRepo(Fakes f) : IPlantAccessRepository
{
    public Task<List<Guid>> PlantIdsOfUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(f.Access.Where(a => a.UserId == userId).Select(a => a.PlantId).ToList());
    public Task<bool> ExistsAsync(Guid plantId, Guid userId, CancellationToken ct = default) => Task.FromResult(f.Access.Any(a => a.PlantId == plantId && a.UserId == userId));
    public Task<List<PlantUserAccess>> ListByPlantAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Access.Where(a => a.PlantId == plantId).ToList());
    public Task<PlantUserAccess?> GetAsync(Guid plantId, Guid userId, CancellationToken ct = default) => Task.FromResult(f.Access.FirstOrDefault(a => a.PlantId == plantId && a.UserId == userId));
    public void Add(PlantUserAccess access) => f.Access.Add(access);
    public void Remove(PlantUserAccess access) => f.Access.Remove(access);
}

internal sealed class Uow(Fakes f) : IMfgUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(++f.Saves);
}

internal sealed class WarehouseClient(Fakes f) : IWarehouseLookupClient
{
    public Task<IReadOnlyList<WarehouseInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => f.Unreachable
            ? throw ERP.MFG.Domain.Common.MfgRules.Fail(ERP.MFG.Domain.Common.MfgErrors.Int001, ("service", "warehouse"))
            : Task.FromResult<IReadOnlyList<WarehouseInfo>>(ids.Where(f.Warehouses.ContainsKey).Select(i => f.Warehouses[i]).ToList());
}

internal sealed class ProductClient(Fakes f) : IProductLookupClient
{
    public Task<IReadOnlyList<ProductInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => f.Unreachable
            ? throw ERP.MFG.Domain.Common.MfgRules.Fail(ERP.MFG.Domain.Common.MfgErrors.Int001, ("service", "product"))
            : Task.FromResult<IReadOnlyList<ProductInfo>>(ids.Where(f.Products.ContainsKey).Select(i => f.Products[i]).ToList());

    public Task<IReadOnlyList<ProductInfo>> LookupBySkuAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default)
        => f.Unreachable
            ? throw ERP.MFG.Domain.Common.MfgRules.Fail(ERP.MFG.Domain.Common.MfgErrors.Int001, ("service", "product"))
            : Task.FromResult<IReadOnlyList<ProductInfo>>(f.Products.Values.Where(p => skus.Contains(p.Sku, StringComparer.OrdinalIgnoreCase)).ToList());
}

internal sealed class Guard(Fakes f) : IOpenOrderGuard
{
    public Task<bool> HasOpenOrdersAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.OpenOrders);
    public Task<bool> HasOpenOperationsAsync(Guid workCenterId, CancellationToken ct = default) => Task.FromResult(f.OpenOrders || f.WorkCenterBusy);
    public Task<(int Count, string? Example)> GetUnfinishedOrdersAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult((f.UnfinishedOrders, f.UnfinishedOrders > 0 ? "MO-2611-00001" : (string?)null));
}

/// <summary>A signed-in user with chosen permissions.</summary>
public sealed class FakeUser(Guid userId, params string[] permissions) : IUserContextService
{
    public UserContext GetCurrentUser() => new() { UserId = userId.ToString(), TenantId = Guid.NewGuid().ToString(), Permissions = permissions };
    public bool HasPermission(string permission) => permissions.Contains(permission);
    public bool HasRole(string role) => false;
    public string GetUserId() => userId.ToString();
    public string GetTenantId() => string.Empty;
}

internal sealed class CatalogFake(Fakes f) : IPlantCatalog
{
    public Task<T?> GetAsync<T>(Guid id, CancellationToken ct = default) where T : BaseEntity, IPlantCoded => Task.FromResult(f.Of<T>().FirstOrDefault(x => x.Id == id && !x.IsDeleted));
    public Task<List<T>> ListAsync<T>(Guid plantId, CancellationToken ct = default) where T : BaseEntity, IPlantCoded => Task.FromResult(f.Of<T>().Where(x => x.PlantId == plantId && !x.IsDeleted).ToList());
    public Task<List<T>> GetManyAsync<T>(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) where T : BaseEntity, IPlantCoded => Task.FromResult(f.Of<T>().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToList());
    public Task<bool> CodeExistsAsync<T>(Guid plantId, string code, CancellationToken ct = default) where T : BaseEntity, IPlantCoded => Task.FromResult(f.Of<T>().Any(x => x.PlantId == plantId && x.Code == code && !x.IsDeleted));
    public void Add<T>(T entity) where T : BaseEntity, IPlantCoded
    {
        f.Of<T>().Add(entity);
        if (entity is WorkCalendar calendar) f.Calendars.Add(calendar);
        if (entity is Crew crew) f.Crews.Add(crew);
    }
}

internal sealed class CalendarFake(Fakes f) : ICalendarRepository
{
    public Task<WorkCalendar?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Calendars.FirstOrDefault(c => c.Id == id && !c.IsDeleted));
    public Task<List<WorkCalendar>> ListAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Calendars.Where(c => c.PlantId == plantId && !c.IsDeleted).ToList());
    public Task<List<WorkCalendar>> UsingShiftAsync(Guid shiftId, CancellationToken ct = default) => Task.FromResult(f.Calendars.Where(c => c.ShiftIds().Contains(shiftId)).ToList());
}

internal sealed class CrewFake(Fakes f) : ICrewRepository
{
    public Task<Crew?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Crews.FirstOrDefault(c => c.Id == id && !c.IsDeleted));
    public Task<List<Crew>> ListAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(f.Crews.Where(c => c.PlantId == plantId && !c.IsDeleted).ToList());
    public Task<bool> HasOverlappingMembershipAsync(Guid employeeId, DateOnly from, DateOnly? to, Guid? exceptMemberId, CancellationToken ct = default)
        => Task.FromResult(f.Crews.SelectMany(c => c.Members).Any(m => m.EmployeeId == employeeId && m.Id != exceptMemberId && CrewMember.Overlaps(from, to, m.ValidFrom, m.ValidTo)));
    public Task<decimal?> OwnRateOnAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
        => Task.FromResult(f.Crews.SelectMany(c => c.Members).Where(m => m.EmployeeId == employeeId && m.LaborRatePerHour != null && m.IsActiveOn(date)).Select(m => m.LaborRatePerHour).FirstOrDefault());
}

internal sealed class ReasonFake(Fakes f) : IReasonCodeRepository
{
    public Task<ReasonCode?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Reasons.FirstOrDefault(r => r.Id == id && !r.IsDeleted));
    public Task<List<ReasonCode>> ListAsync(ReasonType? type, CancellationToken ct = default) => Task.FromResult(f.Reasons.Where(r => !r.IsDeleted && (type == null || r.Type == type)).ToList());
    public Task<bool> ExistsAsync(ReasonType type, string code, CancellationToken ct = default) => Task.FromResult(f.Reasons.Any(r => r.Type == type && r.Code == code && !r.IsDeleted));
    public void Add(ReasonCode reason) => f.Reasons.Add(reason);
}

internal sealed class ReferenceFake(Fakes f) : IReferenceChecker
{
    public Task<bool> ShiftIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.UsedShifts.Contains(id) || f.Calendars.Any(c => c.ShiftIds().Contains(id)));
    public Task<bool> CalendarIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.UsedCalendars.Contains(id) || f.Of<WorkCenter>().Any(w => w.CalendarId == id && !w.IsDeleted));
    public Task<bool> WorkCenterIsUsedAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(f.UsedWorkCenters.Contains(id) || f.Of<Machine>().Any(m => m.WorkCenterId == id && !m.IsDeleted) || f.Of<StandardOperation>().Any(o => o.DefaultWorkCenterId == id && !o.IsDeleted));
    public Task<bool> MachineIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> StandardOperationIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> ReasonIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> CrewIsUsedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> InspectionPlanIsUsedAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(f.Profiles.Any(p => p.FqcInspectionPlanId == id && !p.IsDeleted) || f.Routings.Any(r => !r.IsDeleted && r.Operations.Any(o => o.IpqcInspectionPlanId == id)));
}

internal sealed class EmployeeFake(Fakes f) : IEmployeeLookupClient
{
    public Task<IReadOnlyList<EmployeeInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EmployeeInfo>>(ids.Where(f.Employees.ContainsKey).Select(i => f.Employees[i]).ToList());
}

/// <summary>Builds the calculator from the calendar in the fake store (like the cached service does) and remembers which calendars were invalidated.</summary>
public sealed class RecordingWorkingTime : IWorkingTimeService
{
    public Func<Guid, WorkingTimeCalculator>? Build;
    public readonly List<Guid> Invalidated = [];
    public Task<WorkingTimeCalculator> ForCalendarAsync(Guid calendarId, CancellationToken ct = default) => Task.FromResult(Build!(calendarId));
    public void Invalidate(Guid calendarId) => Invalidated.Add(calendarId);
}
