using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Plants;

public sealed record PlantDto(
    Guid Id, string Code, string Name, string TimeZoneId,
    Guid RawMaterialWarehouseId, Guid FinishedGoodsWarehouseId, Guid QuarantineWarehouseId, Guid? ScrapWarehouseId,
    Guid? DefaultCalendarId, bool IsActive)
{
    public static PlantDto From(Plant p) => new(p.Id, p.Code, p.Name, p.TimeZoneId, p.RawMaterialWarehouseId, p.FinishedGoodsWarehouseId,
        p.QuarantineWarehouseId, p.ScrapWarehouseId, p.DefaultCalendarId, p.IsActive);
}

/// <summary>The plant's warehouses must exist, belong to the caller's tenant and be active (BR-PLT-02). The Warehouse service has no company, so "same company" cannot be checked (risk R-MFG-1).</summary>
internal static class PlantWarehouseCheck
{
    /// <summary>The default calendar, when given, must be a calendar of this plant (BR-PLT-03).</summary>
    public static async Task EnsureCalendarAsync(ICalendarRepository calendars, Plant plant, CancellationToken ct)
    {
        if (plant.DefaultCalendarId is not { } id) return;
        var calendar = await calendars.GetAsync(id, ct);
        if (calendar is null || calendar.PlantId != plant.Id) throw MfgRules.Invalid("The default calendar does not exist in this plant.");
    }

    public static async Task EnsureUsableAsync(IWarehouseLookupClient warehouses, Plant plant, CancellationToken ct)
    {
        var ids = plant.WarehouseIds();
        var found = await warehouses.LookupAsync(ids, ct);
        foreach (var id in ids)
        {
            var w = found.FirstOrDefault(x => x.Id == id);
            if (w is null) throw MfgRules.Invalid($"Warehouse {id} does not exist.");
            if (!w.IsActive) throw MfgRules.Invalid($"Warehouse {w.Code} is not active.");
        }
    }
}

// ── Create ──────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreatePlantCommand(string Code, string Name, string? TimeZoneId, Guid RawMaterialWarehouseId, Guid FinishedGoodsWarehouseId,
    Guid QuarantineWarehouseId, Guid? ScrapWarehouseId, Guid? DefaultCalendarId) : IRequest<PlantDto>;

public sealed class CreatePlantHandler(IPlantRepository plants, IProductionSettingRepository settings, IWarehouseLookupClient warehouses, ICalendarRepository calendars, IMfgUnitOfWork uow)
    : IRequestHandler<CreatePlantCommand, PlantDto>
{
    public async Task<PlantDto> Handle(CreatePlantCommand r, CancellationToken ct)
    {
        // Domain rules first (code format, required warehouses, time zone), then the lookups that cost a call.
        var plant = Plant.Create(r.Code, r.Name, r.TimeZoneId, r.RawMaterialWarehouseId, r.FinishedGoodsWarehouseId, r.QuarantineWarehouseId, r.ScrapWarehouseId, r.DefaultCalendarId);
        if (await plants.CodeExistsAsync(plant.Code, null, ct)) throw MfgRules.Fail(MfgErrors.Gen006, ("code", plant.Code));
        await PlantWarehouseCheck.EnsureUsableAsync(warehouses, plant, ct);
        if (plant.DefaultCalendarId is not null) throw MfgRules.Invalid("A new plant has no calendar yet: create the calendar for the plant, then set it as the default.");

        plants.Add(plant);
        settings.Add(ProductionSetting.CreateDefault(plant.Id)); // BR-SET-01: the plant's one settings row, in the same save
        await uow.SaveChangesAsync(ct);
        return PlantDto.From(plant);
    }
}

// ── Update / activate ───────────────────────────────────────────────────────────────────────────────────────

public sealed record UpdatePlantCommand(Guid Id, string Name, string? TimeZoneId, Guid RawMaterialWarehouseId, Guid FinishedGoodsWarehouseId,
    Guid QuarantineWarehouseId, Guid? ScrapWarehouseId, Guid? DefaultCalendarId) : IRequest<PlantDto>;

public sealed class UpdatePlantHandler(IPlantAccessService access, IWarehouseLookupClient warehouses, ICalendarRepository calendars, IMfgUnitOfWork uow)
    : IRequestHandler<UpdatePlantCommand, PlantDto>
{
    public async Task<PlantDto> Handle(UpdatePlantCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.Id, ct);
        plant.Update(r.Name, r.TimeZoneId, r.RawMaterialWarehouseId, r.FinishedGoodsWarehouseId, r.QuarantineWarehouseId, r.ScrapWarehouseId, r.DefaultCalendarId);
        await PlantWarehouseCheck.EnsureUsableAsync(warehouses, plant, ct);
        await PlantWarehouseCheck.EnsureCalendarAsync(calendars, plant, ct);
        await uow.SaveChangesAsync(ct);
        return PlantDto.From(plant);
    }
}

public sealed record SetPlantActiveCommand(Guid Id, bool Active) : IRequest<PlantDto>;

public sealed class SetPlantActiveHandler(IPlantAccessService access, IOpenOrderGuard orders, IMfgUnitOfWork uow) : IRequestHandler<SetPlantActiveCommand, PlantDto>
{
    public async Task<PlantDto> Handle(SetPlantActiveCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.Id, ct);
        if (!r.Active)
        {
            // R219: an inactive plant hides its orders (404), so none may be left unfinished
            var (count, example) = await orders.GetUnfinishedOrdersAsync(plant.Id, ct);
            MfgRules.ThrowIf(count > 0, MfgErrors.Plt001, ("count", count), ("order", example ?? ""));
        }
        plant.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return PlantDto.From(plant);
    }
}

// ── Read ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPlantsQuery(bool ActiveOnly = false) : IRequest<List<PlantDto>>;

public sealed class GetPlantsHandler(IPlantRepository plants, IPlantAccessService access) : IRequestHandler<GetPlantsQuery, List<PlantDto>>
{
    public async Task<List<PlantDto>> Handle(GetPlantsQuery r, CancellationToken ct)
    {
        var visible = await access.VisiblePlantIdsAsync(ct);
        var list = await plants.ListAsync(visible, r.ActiveOnly, ct);
        return list.OrderBy(p => p.Code).Select(PlantDto.From).ToList();
    }
}

public sealed record GetPlantQuery(Guid Id) : IRequest<PlantDto>;

public sealed class GetPlantHandler(IPlantAccessService access) : IRequestHandler<GetPlantQuery, PlantDto>
{
    public async Task<PlantDto> Handle(GetPlantQuery r, CancellationToken ct) => PlantDto.From(await access.GetVisiblePlantAsync(r.Id, ct));
}

// ── Who may work in a plant (BR-GEN-09) ─────────────────────────────────────────────────────────────────────

public sealed record PlantAccessDto(Guid PlantId, Guid UserId);

public sealed record GrantPlantAccessCommand(Guid PlantId, Guid UserId) : IRequest<PlantAccessDto>;

public sealed class GrantPlantAccessHandler(IPlantAccessService access, IPlantAccessRepository repo, IMfgUnitOfWork uow)
    : IRequestHandler<GrantPlantAccessCommand, PlantAccessDto>
{
    public async Task<PlantAccessDto> Handle(GrantPlantAccessCommand r, CancellationToken ct)
    {
        if (r.UserId == Guid.Empty) throw MfgRules.Invalid("A user is required.");
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        if (!await repo.ExistsAsync(plant.Id, r.UserId, ct))
        {
            repo.Add(PlantUserAccess.Grant(plant.Id, r.UserId));
            await uow.SaveChangesAsync(ct);
        }
        return new PlantAccessDto(plant.Id, r.UserId);
    }
}

public sealed record RevokePlantAccessCommand(Guid PlantId, Guid UserId) : IRequest<Unit>;

public sealed class RevokePlantAccessHandler(IPlantAccessService access, IPlantAccessRepository repo, IMfgUnitOfWork uow)
    : IRequestHandler<RevokePlantAccessCommand, Unit>
{
    public async Task<Unit> Handle(RevokePlantAccessCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var row = await repo.GetAsync(plant.Id, r.UserId, ct) ?? throw new EntityNotFoundException("PlantUserAccess", r.UserId);
        repo.Remove(row);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record ListPlantAccessQuery(Guid PlantId) : IRequest<List<PlantAccessDto>>;

public sealed class ListPlantAccessHandler(IPlantAccessService access, IPlantAccessRepository repo) : IRequestHandler<ListPlantAccessQuery, List<PlantAccessDto>>
{
    public async Task<List<PlantAccessDto>> Handle(ListPlantAccessQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await repo.ListByPlantAsync(plant.Id, ct)).Select(a => new PlantAccessDto(a.PlantId, a.UserId)).ToList();
    }
}
