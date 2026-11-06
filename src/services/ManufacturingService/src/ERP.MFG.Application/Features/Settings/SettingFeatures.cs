using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Settings;

public sealed record SettingsDto(Guid PlantId, ProductionSettingValues Values);

public sealed record SettingAuditDto(string Field, string OldValue, string NewValue, string ChangedBy, DateTime ChangedAt);

// ── Read ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetSettingsQuery(Guid PlantId) : IRequest<SettingsDto>;

public sealed class GetSettingsHandler(IPlantAccessService access, IProductionSettingProvider provider) : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return new SettingsDto(plant.Id, await provider.GetAsync(plant.Id, ct));
    }
}

public sealed record GetSettingsAuditQuery(Guid PlantId, int Take = 100) : IRequest<List<SettingAuditDto>>;

public sealed class GetSettingsAuditHandler(IPlantAccessService access, IProductionSettingRepository repo) : IRequestHandler<GetSettingsAuditQuery, List<SettingAuditDto>>
{
    public async Task<List<SettingAuditDto>> Handle(GetSettingsAuditQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var rows = await repo.GetAuditAsync(plant.Id, Math.Clamp(r.Take, 1, 500), ct);
        return rows.Select(a => new SettingAuditDto(a.Field, a.OldValue, a.NewValue, a.ChangedBy, a.ChangedAt)).ToList();
    }
}

// ── Update ──────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record UpdateSettingsCommand(Guid PlantId, ProductionSettingValues Values) : IRequest<SettingsDto>;

public sealed class UpdateSettingsHandler(
    IPlantAccessService access,
    IProductionSettingRepository repo,
    IOpenOrderGuard openOrders,
    IProductionSettingProvider provider,
    IUserContextService user,
    IMfgUnitOfWork uow) : IRequestHandler<UpdateSettingsCommand, SettingsDto>
{
    public async Task<SettingsDto> Handle(UpdateSettingsCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var setting = await repo.GetByPlantAsync(plant.Id, ct) ?? throw new EntityNotFoundException(nameof(ProductionSetting), plant.Id);

        // Only ask the order guard when the costing method really changes (BR-SET-03).
        var costingChanges = r.Values.CostingMethod != setting.CostingMethod;
        var openOrdersExist = costingChanges && await openOrders.HasOpenOrdersAsync(plant.Id, ct);

        var changes = setting.Update(r.Values, openOrdersExist);
        if (changes.Count > 0)
        {
            var by = user.GetUserId();
            foreach (var change in changes) repo.AddAudit(ProductionSettingAudit.Record(plant.Id, change, by));
            await uow.SaveChangesAsync(ct);
            provider.Invalidate(plant.Id); // BR-SET-02: effective for transactions after the save
        }
        return new SettingsDto(plant.Id, setting.ToValues());
    }
}
