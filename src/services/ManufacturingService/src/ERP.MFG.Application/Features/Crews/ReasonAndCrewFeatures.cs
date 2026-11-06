using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Calendars;
using ERP.MFG.Application.Features.WorkCenters;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Crews;

// ── Reason codes (A09) ───────────────────────────────────────────────────────────────────────────────────────

public sealed record ReasonCodeDto(Guid Id, ReasonType Type, string Code, string Name, DowntimeCategory? Category, bool IsActive)
{
    public static ReasonCodeDto From(ReasonCode r) => new(r.Id, r.Type, r.Code, r.Name, r.Category, r.IsActive);
}

/// <summary>Adds the default reason codes (T1.5.1) the tenant does not have yet. Idempotent; also run once when a tenant reads its reasons and has none.</summary>
internal static class ReasonSeeder
{
    public static async Task<int> SeedAsync(IReasonCodeRepository repo, CancellationToken ct)
    {
        var added = 0;
        foreach (var (type, code, name, category) in ReasonCode.DefaultSeeds)
        {
            if (await repo.ExistsAsync(type, code, ct)) continue;
            repo.Add(ReasonCode.Create(type, code, name, category));
            added++;
        }
        return added;
    }
}

public sealed record SeedReasonCodesCommand : IRequest<int>;

public sealed class SeedReasonCodesHandler(IReasonCodeRepository repo, IMfgUnitOfWork uow) : IRequestHandler<SeedReasonCodesCommand, int>
{
    public async Task<int> Handle(SeedReasonCodesCommand r, CancellationToken ct)
    {
        var added = await ReasonSeeder.SeedAsync(repo, ct);
        if (added > 0) await uow.SaveChangesAsync(ct);
        return added;
    }
}

public sealed record GetReasonCodesQuery(ReasonType? Type = null) : IRequest<List<ReasonCodeDto>>;

public sealed class GetReasonCodesHandler(IReasonCodeRepository repo, IMfgUnitOfWork uow) : IRequestHandler<GetReasonCodesQuery, List<ReasonCodeDto>>
{
    public async Task<List<ReasonCodeDto>> Handle(GetReasonCodesQuery r, CancellationToken ct)
    {
        var all = await repo.ListAsync(null, ct);
        if (all.Count == 0 && await ReasonSeeder.SeedAsync(repo, ct) > 0) // first read of a new tenant
        {
            await uow.SaveChangesAsync(ct);
            all = await repo.ListAsync(null, ct);
        }
        return all.Where(x => r.Type is null || x.Type == r.Type).OrderBy(x => x.Type).ThenBy(x => x.Code).Select(ReasonCodeDto.From).ToList();
    }
}

public sealed record CreateReasonCodeCommand(ReasonType Type, string Code, string Name, DowntimeCategory? Category) : IRequest<ReasonCodeDto>;

public sealed class CreateReasonCodeHandler(IReasonCodeRepository repo, IMfgUnitOfWork uow) : IRequestHandler<CreateReasonCodeCommand, ReasonCodeDto>
{
    public async Task<ReasonCodeDto> Handle(CreateReasonCodeCommand r, CancellationToken ct)
    {
        var reason = ReasonCode.Create(r.Type, r.Code, r.Name, r.Category);
        if (await repo.ExistsAsync(reason.Type, reason.Code, ct)) throw MfgRules.Fail(MfgErrors.Gen006, ("code", reason.Code)); // BR-RSN-01: unique per type
        repo.Add(reason);
        await uow.SaveChangesAsync(ct);
        return ReasonCodeDto.From(reason);
    }
}

public sealed record UpdateReasonCodeCommand(Guid Id, string Name, DowntimeCategory? Category) : IRequest<ReasonCodeDto>;

public sealed class UpdateReasonCodeHandler(IReasonCodeRepository repo, IMfgUnitOfWork uow) : IRequestHandler<UpdateReasonCodeCommand, ReasonCodeDto>
{
    public async Task<ReasonCodeDto> Handle(UpdateReasonCodeCommand r, CancellationToken ct)
    {
        var reason = await repo.GetAsync(r.Id, ct) ?? throw new EntityNotFoundException(nameof(ReasonCode), r.Id);
        reason.Update(r.Name, r.Category);
        await uow.SaveChangesAsync(ct);
        return ReasonCodeDto.From(reason);
    }
}

public sealed record SetReasonCodeActiveCommand(Guid Id, bool Active) : IRequest<ReasonCodeDto>;

public sealed class SetReasonCodeActiveHandler(IReasonCodeRepository repo, IMfgUnitOfWork uow) : IRequestHandler<SetReasonCodeActiveCommand, ReasonCodeDto>
{
    public async Task<ReasonCodeDto> Handle(SetReasonCodeActiveCommand r, CancellationToken ct)
    {
        var reason = await repo.GetAsync(r.Id, ct) ?? throw new EntityNotFoundException(nameof(ReasonCode), r.Id);
        reason.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return ReasonCodeDto.From(reason);
    }
}

public sealed record DeleteReasonCodeCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteReasonCodeHandler(IReasonCodeRepository repo, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteReasonCodeCommand, Unit>
{
    public async Task<Unit> Handle(DeleteReasonCodeCommand r, CancellationToken ct)
    {
        var reason = await repo.GetAsync(r.Id, ct) ?? throw new EntityNotFoundException(nameof(ReasonCode), r.Id);
        if (await references.ReasonIsUsedAsync(reason.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        reason.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Crews (A10) ──────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CrewMemberDto(Guid Id, Guid EmployeeId, string? EmployeeNumber, string? EmployeeName, DateOnly ValidFrom, DateOnly? ValidTo, decimal? LaborRatePerHour);

public sealed record CrewDto(Guid Id, Guid PlantId, string Code, string Name, Guid? WorkCenterId, Guid? LeaderEmployeeId, bool IsActive, List<CrewMemberDto> Members)
{
    public static CrewDto From(Crew c, IReadOnlyDictionary<Guid, EmployeeInfo>? employees = null) => new(c.Id, c.PlantId, c.Code, c.Name, c.WorkCenterId, c.LeaderEmployeeId, c.IsActive,
        c.Members.OrderBy(m => m.ValidFrom).Select(m =>
        {
            EmployeeInfo? e = null;
            employees?.TryGetValue(m.EmployeeId, out e);
            return new CrewMemberDto(m.Id, m.EmployeeId, e?.EmployeeNumber, e?.FullName, m.ValidFrom, m.ValidTo, m.LaborRatePerHour);
        }).ToList());
}

internal static class CrewRules
{
    public static async Task EnsureEmployeeAsync(IEmployeeLookupClient employees, Guid employeeId, string what, CancellationToken ct)
    {
        var e = (await employees.LookupAsync([employeeId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"The {what} (employee {employeeId}) does not exist.");
        if (!e.IsActive) throw MfgRules.Invalid($"The {what} {e.EmployeeNumber} is not an active employee.");
    }

    public static async Task<Crew> LoadAsync(ICrewRepository crews, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var crew = await crews.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Crew), id);
        await access.GetVisiblePlantAsync(crew.PlantId, ct);
        return crew;
    }

    public static async Task EnsureWorkCenterAsync(IPlantCatalog catalog, Guid plantId, Guid? workCenterId, CancellationToken ct)
    {
        if (workCenterId is { } id && id != Guid.Empty) await WorkCenterRules.InPlantAsync(catalog, plantId, id, ct);
    }
}

public sealed record CreateCrewCommand(Guid PlantId, string Code, string Name, Guid? WorkCenterId, Guid? LeaderEmployeeId) : IRequest<CrewDto>;

public sealed class CreateCrewHandler(IPlantAccessService access, IPlantCatalog catalog, IEmployeeLookupClient employees, IMfgUnitOfWork uow) : IRequestHandler<CreateCrewCommand, CrewDto>
{
    public async Task<CrewDto> Handle(CreateCrewCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var crew = Crew.Create(plant.Id, r.Code, r.Name, r.WorkCenterId, r.LeaderEmployeeId);
        await PlantScope.EnsureCodeFreeAsync<Crew>(catalog, plant.Id, crew.Code, ct);
        await CrewRules.EnsureWorkCenterAsync(catalog, plant.Id, crew.WorkCenterId, ct);
        if (crew.LeaderEmployeeId is { } leader) await CrewRules.EnsureEmployeeAsync(employees, leader, "leader", ct);
        catalog.Add(crew);
        await uow.SaveChangesAsync(ct);
        return CrewDto.From(crew);
    }
}

public sealed record UpdateCrewCommand(Guid Id, string Name, Guid? WorkCenterId, Guid? LeaderEmployeeId) : IRequest<CrewDto>;

public sealed class UpdateCrewHandler(IPlantAccessService access, ICrewRepository crews, IPlantCatalog catalog, IEmployeeLookupClient employees, IMfgUnitOfWork uow) : IRequestHandler<UpdateCrewCommand, CrewDto>
{
    public async Task<CrewDto> Handle(UpdateCrewCommand r, CancellationToken ct)
    {
        var crew = await CrewRules.LoadAsync(crews, access, r.Id, ct);
        crew.Update(r.Name, r.WorkCenterId, r.LeaderEmployeeId);
        await CrewRules.EnsureWorkCenterAsync(catalog, crew.PlantId, crew.WorkCenterId, ct);
        if (crew.LeaderEmployeeId is { } leader) await CrewRules.EnsureEmployeeAsync(employees, leader, "leader", ct);
        await uow.SaveChangesAsync(ct);
        return CrewDto.From(crew);
    }
}

public sealed record SetCrewActiveCommand(Guid Id, bool Active) : IRequest<CrewDto>;

public sealed class SetCrewActiveHandler(IPlantAccessService access, ICrewRepository crews, IMfgUnitOfWork uow) : IRequestHandler<SetCrewActiveCommand, CrewDto>
{
    public async Task<CrewDto> Handle(SetCrewActiveCommand r, CancellationToken ct)
    {
        var crew = await CrewRules.LoadAsync(crews, access, r.Id, ct);
        crew.SetActive(r.Active);
        await uow.SaveChangesAsync(ct);
        return CrewDto.From(crew);
    }
}

public sealed record DeleteCrewCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteCrewHandler(IPlantAccessService access, ICrewRepository crews, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteCrewCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCrewCommand r, CancellationToken ct)
    {
        var crew = await CrewRules.LoadAsync(crews, access, r.Id, ct);
        if (await references.CrewIsUsedAsync(crew.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        crew.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record AddCrewMemberCommand(Guid CrewId, Guid EmployeeId, DateOnly ValidFrom, DateOnly? ValidTo, decimal? LaborRatePerHour) : IRequest<CrewDto>;

public sealed class AddCrewMemberHandler(IPlantAccessService access, ICrewRepository crews, IEmployeeLookupClient employees, IMfgUnitOfWork uow) : IRequestHandler<AddCrewMemberCommand, CrewDto>
{
    public async Task<CrewDto> Handle(AddCrewMemberCommand r, CancellationToken ct)
    {
        var crew = await CrewRules.LoadAsync(crews, access, r.CrewId, ct);
        await CrewRules.EnsureEmployeeAsync(employees, r.EmployeeId, "crew member", ct);

        // BR-LBR-01: an employee is in at most one crew at any time.
        if (await crews.HasOverlappingMembershipAsync(r.EmployeeId, r.ValidFrom, r.ValidTo, null, ct))
            throw MfgRules.Invalid("The employee already belongs to a crew in that period.");

        crew.AddMember(r.EmployeeId, r.ValidFrom, r.ValidTo, r.LaborRatePerHour);
        await uow.SaveChangesAsync(ct);
        var names = (await employees.LookupAsync(crew.Members.Select(m => m.EmployeeId).Distinct().ToList(), ct)).ToDictionary(e => e.Id);
        return CrewDto.From(crew, names);
    }
}

public sealed record RemoveCrewMemberCommand(Guid CrewId, Guid MemberId) : IRequest<CrewDto>;

public sealed class RemoveCrewMemberHandler(IPlantAccessService access, ICrewRepository crews, IMfgUnitOfWork uow) : IRequestHandler<RemoveCrewMemberCommand, CrewDto>
{
    public async Task<CrewDto> Handle(RemoveCrewMemberCommand r, CancellationToken ct)
    {
        var crew = await CrewRules.LoadAsync(crews, access, r.CrewId, ct);
        if (!crew.RemoveMember(r.MemberId)) throw new EntityNotFoundException(nameof(CrewMember), r.MemberId);
        await uow.SaveChangesAsync(ct);
        return CrewDto.From(crew);
    }
}

public sealed record GetCrewsQuery(Guid PlantId) : IRequest<List<CrewDto>>;

public sealed class GetCrewsHandler(IPlantAccessService access, ICrewRepository crews, IEmployeeLookupClient employees) : IRequestHandler<GetCrewsQuery, List<CrewDto>>
{
    public async Task<List<CrewDto>> Handle(GetCrewsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var list = (await crews.ListAsync(plant.Id, ct)).OrderBy(c => c.Code).ToList();
        var ids = list.SelectMany(c => c.Members.Select(m => m.EmployeeId)).Distinct().ToList();
        var names = ids.Count == 0 ? new Dictionary<Guid, EmployeeInfo>() : (await employees.LookupAsync(ids, ct)).ToDictionary(e => e.Id);
        return list.Select(c => CrewDto.From(c, names)).ToList();
    }
}
