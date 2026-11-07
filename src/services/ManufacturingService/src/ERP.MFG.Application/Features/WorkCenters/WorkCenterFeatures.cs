using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Calendars;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.WorkCenters;

// ── Work centers (A06) ───────────────────────────────────────────────────────────────────────────────────────

public sealed record WorkCenterDto(Guid Id, Guid PlantId, string Code, bool IsActive, WorkCenterValues Values)
{
    public static WorkCenterDto From(WorkCenter w) => new(w.Id, w.PlantId, w.Code, w.IsActive, w.ToValues());
}

internal static class WorkCenterRules
{
    /// <summary>The calendar of a work center must exist in the plant and be active (T1.2).</summary>
    public static async Task EnsureCalendarAsync(ICalendarRepository calendars, Guid plantId, Guid calendarId, CancellationToken ct)
    {
        var calendar = await calendars.GetAsync(calendarId, ct);
        if (calendar is null || calendar.PlantId != plantId) throw MfgRules.Invalid("The work calendar does not exist in this plant.");
        if (!calendar.IsActive) throw MfgRules.Invalid($"Work calendar {calendar.Code} is not active.");
    }

    /// <summary>A machine / standard operation / crew points at a work center of its own plant.</summary>
    public static async Task<WorkCenter> InPlantAsync(IPlantCatalog catalog, Guid plantId, Guid workCenterId, CancellationToken ct)
    {
        var center = await catalog.GetAsync<WorkCenter>(workCenterId, ct);
        if (center is null || center.PlantId != plantId) throw MfgRules.Invalid("The work center does not exist in this plant.");
        return center;
    }
}

public sealed record CreateWorkCenterCommand(Guid PlantId, string Code, WorkCenterValues Values) : IRequest<WorkCenterDto>;

public sealed class CreateWorkCenterHandler(IPlantAccessService access, IPlantCatalog catalog, ICalendarRepository calendars, IMfgUnitOfWork uow) : IRequestHandler<CreateWorkCenterCommand, WorkCenterDto>
{
    public async Task<WorkCenterDto> Handle(CreateWorkCenterCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var center = WorkCenter.Create(plant.Id, r.Code, r.Values);
        await PlantScope.EnsureCodeFreeAsync<WorkCenter>(catalog, plant.Id, center.Code, ct);
        await WorkCenterRules.EnsureCalendarAsync(calendars, plant.Id, center.CalendarId, ct);
        catalog.Add(center);
        await uow.SaveChangesAsync(ct);
        return WorkCenterDto.From(center);
    }
}

public sealed record UpdateWorkCenterCommand(Guid Id, WorkCenterValues Values) : IRequest<WorkCenterDto>;

public sealed class UpdateWorkCenterHandler(IPlantAccessService access, IPlantCatalog catalog, ICalendarRepository calendars, IMfgUnitOfWork uow) : IRequestHandler<UpdateWorkCenterCommand, WorkCenterDto>
{
    public async Task<WorkCenterDto> Handle(UpdateWorkCenterCommand r, CancellationToken ct)
    {
        var center = await PlantScope.LoadAsync<WorkCenter>(catalog, access, r.Id, ct);
        center.Update(r.Values);
        await WorkCenterRules.EnsureCalendarAsync(calendars, center.PlantId, center.CalendarId, ct);
        await uow.SaveChangesAsync(ct);
        return WorkCenterDto.From(center);
    }
}

public sealed record SetWorkCenterActiveCommand(Guid Id, bool Active) : IRequest<WorkCenterDto>;

public sealed class SetWorkCenterActiveHandler(IPlantAccessService access, IPlantCatalog catalog, IOpenOrderGuard orders, IMfgUnitOfWork uow) : IRequestHandler<SetWorkCenterActiveCommand, WorkCenterDto>
{
    public async Task<WorkCenterDto> Handle(SetWorkCenterActiveCommand r, CancellationToken ct)
    {
        var center = await PlantScope.LoadAsync<WorkCenter>(catalog, access, r.Id, ct);
        var open = !r.Active && await orders.HasOpenOperationsAsync(center.Id, ct); // BR-WC-03
        center.SetActive(r.Active, open);
        await uow.SaveChangesAsync(ct);
        return WorkCenterDto.From(center);
    }
}

public sealed record DeleteWorkCenterCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteWorkCenterHandler(IPlantAccessService access, IPlantCatalog catalog, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteWorkCenterCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorkCenterCommand r, CancellationToken ct)
    {
        var center = await PlantScope.LoadAsync<WorkCenter>(catalog, access, r.Id, ct);
        if (await references.WorkCenterIsUsedAsync(center.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005); // BR-GEN-02: used -> deactivate instead
        center.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record GetWorkCentersQuery(Guid PlantId) : IRequest<List<WorkCenterDto>>;

public sealed class GetWorkCentersHandler(IPlantAccessService access, IPlantCatalog catalog) : IRequestHandler<GetWorkCentersQuery, List<WorkCenterDto>>
{
    public async Task<List<WorkCenterDto>> Handle(GetWorkCentersQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await catalog.ListAsync<WorkCenter>(plant.Id, ct)).OrderBy(w => w.Code).Select(WorkCenterDto.From).ToList();
    }
}

public sealed record GetWorkCenterQuery(Guid Id) : IRequest<WorkCenterDto>;

public sealed class GetWorkCenterHandler(IPlantAccessService access, IPlantCatalog catalog) : IRequestHandler<GetWorkCenterQuery, WorkCenterDto>
{
    public async Task<WorkCenterDto> Handle(GetWorkCenterQuery r, CancellationToken ct) => WorkCenterDto.From(await PlantScope.LoadAsync<WorkCenter>(catalog, access, r.Id, ct));
}

/// <summary>BR-WC-02 for a date range (the chart of S05): working minutes and standard-minute capacity per day.</summary>
public sealed record GetCapacityQuery(Guid WorkCenterId, DateOnly From, DateOnly To) : IRequest<IReadOnlyList<DayCapacity>>;

public sealed class GetCapacityHandler(IPlantAccessService access, IPlantCatalog catalog, ICapacityService capacity) : IRequestHandler<GetCapacityQuery, IReadOnlyList<DayCapacity>>
{
    public async Task<IReadOnlyList<DayCapacity>> Handle(GetCapacityQuery r, CancellationToken ct)
    {
        var center = await PlantScope.LoadAsync<WorkCenter>(catalog, access, r.WorkCenterId, ct);
        return await capacity.GetDailyCapacityAsync(center.Id, r.From, r.To, ct);
    }
}

// ── Machines (A07) ───────────────────────────────────────────────────────────────────────────────────────────

public sealed record MachineDto(Guid Id, Guid PlantId, string Code, string Name, Guid WorkCenterId, MachineStatus Status, decimal IdealCycleTimeSeconds, string? SerialNo, bool IsActive)
{
    public static MachineDto From(Machine m) => new(m.Id, m.PlantId, m.Code, m.Name, m.WorkCenterId, m.Status, m.IdealCycleTimeSeconds, m.SerialNo, m.IsActive);
}

public sealed record CreateMachineCommand(Guid PlantId, string Code, string Name, Guid WorkCenterId, decimal IdealCycleTimeSeconds, string? SerialNo) : IRequest<MachineDto>;

public sealed class CreateMachineHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<CreateMachineCommand, MachineDto>
{
    public async Task<MachineDto> Handle(CreateMachineCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var machine = Machine.Create(plant.Id, r.Code, r.Name, r.WorkCenterId, r.IdealCycleTimeSeconds, r.SerialNo);
        await PlantScope.EnsureCodeFreeAsync<Machine>(catalog, plant.Id, machine.Code, ct);
        await WorkCenterRules.InPlantAsync(catalog, plant.Id, r.WorkCenterId, ct);
        catalog.Add(machine);
        await uow.SaveChangesAsync(ct);
        return MachineDto.From(machine);
    }
}

public sealed record UpdateMachineCommand(Guid Id, string Name, Guid WorkCenterId, decimal IdealCycleTimeSeconds, string? SerialNo) : IRequest<MachineDto>;

public sealed class UpdateMachineHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<UpdateMachineCommand, MachineDto>
{
    public async Task<MachineDto> Handle(UpdateMachineCommand r, CancellationToken ct)
    {
        var machine = await PlantScope.LoadAsync<Machine>(catalog, access, r.Id, ct);
        machine.Update(r.Name, r.WorkCenterId, r.IdealCycleTimeSeconds, r.SerialNo);
        await WorkCenterRules.InPlantAsync(catalog, machine.PlantId, r.WorkCenterId, ct);
        await uow.SaveChangesAsync(ct);
        return MachineDto.From(machine);
    }
}

public sealed record SetMachineStatusCommand(Guid Id, MachineStatus Status) : IRequest<MachineDto>;

public sealed class SetMachineStatusHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<SetMachineStatusCommand, MachineDto>
{
    public async Task<MachineDto> Handle(SetMachineStatusCommand r, CancellationToken ct)
    {
        var machine = await PlantScope.LoadAsync<Machine>(catalog, access, r.Id, ct);
        machine.SetStatus(r.Status);
        await uow.SaveChangesAsync(ct);
        return MachineDto.From(machine);
    }
}

public sealed record SetMachineActiveCommand(Guid Id, bool Active) : IRequest<MachineDto>;

public sealed class SetMachineActiveHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<SetMachineActiveCommand, MachineDto>
{
    public async Task<MachineDto> Handle(SetMachineActiveCommand r, CancellationToken ct)
    {
        var machine = await PlantScope.LoadAsync<Machine>(catalog, access, r.Id, ct);
        machine.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return MachineDto.From(machine);
    }
}

public sealed record DeleteMachineCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteMachineHandler(IPlantAccessService access, IPlantCatalog catalog, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteMachineCommand, Unit>
{
    public async Task<Unit> Handle(DeleteMachineCommand r, CancellationToken ct)
    {
        var machine = await PlantScope.LoadAsync<Machine>(catalog, access, r.Id, ct);
        if (await references.MachineIsUsedAsync(machine.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        machine.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record GetMachinesQuery(Guid PlantId, Guid? WorkCenterId = null) : IRequest<List<MachineDto>>;

public sealed class GetMachinesHandler(IPlantAccessService access, IPlantCatalog catalog) : IRequestHandler<GetMachinesQuery, List<MachineDto>>
{
    public async Task<List<MachineDto>> Handle(GetMachinesQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await catalog.ListAsync<Machine>(plant.Id, ct)).Where(m => r.WorkCenterId is null || m.WorkCenterId == r.WorkCenterId)
            .OrderBy(m => m.Code).Select(MachineDto.From).ToList();
    }
}

// ── Standard operations (A08) ────────────────────────────────────────────────────────────────────────────────

public sealed record StandardOperationDto(Guid Id, Guid PlantId, string Code, string Name, Guid DefaultWorkCenterId, decimal SetupTimeMinutes, decimal RunTimeMinutes,
    RunTimeBasis RunTimeBasis, decimal? BatchSize, string? Instructions, bool IsActive)
{
    public static StandardOperationDto From(StandardOperation o) => new(o.Id, o.PlantId, o.Code, o.Name, o.DefaultWorkCenterId, o.SetupTimeMinutes, o.RunTimeMinutes,
        o.RunTimeBasis, o.BatchSize, o.Instructions, o.IsActive);
}

public sealed record StandardOperationBody(string Name, Guid DefaultWorkCenterId, decimal SetupTimeMinutes, decimal RunTimeMinutes, RunTimeBasis RunTimeBasis, decimal? BatchSize, string? Instructions);

public sealed record CreateStandardOperationCommand(Guid PlantId, string Code, StandardOperationBody Body) : IRequest<StandardOperationDto>;

public sealed class CreateStandardOperationHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<CreateStandardOperationCommand, StandardOperationDto>
{
    public async Task<StandardOperationDto> Handle(CreateStandardOperationCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var b = r.Body;
        var op = StandardOperation.Create(plant.Id, r.Code, b.Name, b.DefaultWorkCenterId, b.SetupTimeMinutes, b.RunTimeMinutes, b.RunTimeBasis, b.BatchSize, b.Instructions);
        await PlantScope.EnsureCodeFreeAsync<StandardOperation>(catalog, plant.Id, op.Code, ct);
        await WorkCenterRules.InPlantAsync(catalog, plant.Id, b.DefaultWorkCenterId, ct);
        catalog.Add(op);
        await uow.SaveChangesAsync(ct);
        return StandardOperationDto.From(op);
    }
}

public sealed record UpdateStandardOperationCommand(Guid Id, StandardOperationBody Body) : IRequest<StandardOperationDto>;

public sealed class UpdateStandardOperationHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<UpdateStandardOperationCommand, StandardOperationDto>
{
    public async Task<StandardOperationDto> Handle(UpdateStandardOperationCommand r, CancellationToken ct)
    {
        var op = await PlantScope.LoadAsync<StandardOperation>(catalog, access, r.Id, ct);
        var b = r.Body;
        op.Update(b.Name, b.DefaultWorkCenterId, b.SetupTimeMinutes, b.RunTimeMinutes, b.RunTimeBasis, b.BatchSize, b.Instructions);
        await WorkCenterRules.InPlantAsync(catalog, op.PlantId, b.DefaultWorkCenterId, ct);
        await uow.SaveChangesAsync(ct);
        return StandardOperationDto.From(op);
    }
}

public sealed record SetStandardOperationActiveCommand(Guid Id, bool Active) : IRequest<StandardOperationDto>;

public sealed class SetStandardOperationActiveHandler(IPlantAccessService access, IPlantCatalog catalog, IMfgUnitOfWork uow) : IRequestHandler<SetStandardOperationActiveCommand, StandardOperationDto>
{
    public async Task<StandardOperationDto> Handle(SetStandardOperationActiveCommand r, CancellationToken ct)
    {
        var op = await PlantScope.LoadAsync<StandardOperation>(catalog, access, r.Id, ct);
        op.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return StandardOperationDto.From(op);
    }
}

public sealed record DeleteStandardOperationCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteStandardOperationHandler(IPlantAccessService access, IPlantCatalog catalog, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteStandardOperationCommand, Unit>
{
    public async Task<Unit> Handle(DeleteStandardOperationCommand r, CancellationToken ct)
    {
        var op = await PlantScope.LoadAsync<StandardOperation>(catalog, access, r.Id, ct);
        if (await references.StandardOperationIsUsedAsync(op.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        op.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record GetStandardOperationsQuery(Guid PlantId) : IRequest<List<StandardOperationDto>>;

public sealed class GetStandardOperationsHandler(IPlantAccessService access, IPlantCatalog catalog) : IRequestHandler<GetStandardOperationsQuery, List<StandardOperationDto>>
{
    public async Task<List<StandardOperationDto>> Handle(GetStandardOperationsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await catalog.ListAsync<StandardOperation>(plant.Id, ct)).OrderBy(o => o.Code).Select(StandardOperationDto.From).ToList();
    }
}
