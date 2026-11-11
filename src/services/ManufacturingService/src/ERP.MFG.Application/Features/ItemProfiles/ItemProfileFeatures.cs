using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using MediatR;

namespace ERP.MFG.Application.Features.ItemProfiles;

/// <summary>The profile of an item in a plant; <see cref="IsDefault"/> is true when nobody saved one yet and the documented defaults are shown.</summary>
public sealed record ItemProfileDto(Guid ItemId, Guid PlantId, string ItemCode, string ItemName, bool IsDefault, ItemProfileValues Values, int LowLevelCode, decimal StandardCost);

public sealed record ItemProfileListItemDto(Guid ItemId, string ItemCode, string ItemName, MakeOrBuy MakeOrBuy, LotSizingPolicy LotSizingPolicy, int LowLevelCode);

public sealed record PagedProfiles(List<ItemProfileListItemDto> Items, int Total, int Page, int PageSize);

// ── Save ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record UpsertItemProfileCommand(Guid ItemId, Guid PlantId, ItemProfileValues Values) : IRequest<ItemProfileDto>;

public sealed class UpsertItemProfileHandler(IPlantAccessService access, IItemProfileRepository repo, IProductLookupClient products, IInspectionPlanRepository plans, IMfgUnitOfWork uow)
    : IRequestHandler<UpsertItemProfileCommand, ItemProfileDto>
{
    public async Task<ItemProfileDto> Handle(UpsertItemProfileCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        ItemManufacturingProfile.Validate(r.Values); // the cheap checks before the call to ProductService

        // BR-ITM-01: the item exists, is Active and counts in a base unit of measure.
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault()
                   ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        if (!item.IsActive) throw MfgRules.Invalid($"Item {item.Sku} is not active.");
        if (item.BaseUomId is null) throw MfgRules.Invalid($"Item {item.Sku} has no base unit of measure.");

        // P4-3: the final inspection plan must be an FQC plan of this item in this plant
        await InspectionPlanPointer.CheckAsync(plans, r.Values.RequiresFqc ? r.Values.FqcInspectionPlanId : null, plant.Id, r.ItemId, InspectionType.Fqc, ct);

        var profile = await repo.GetAsync(r.ItemId, plant.Id, ct);
        if (profile is null)
        {
            profile = ItemManufacturingProfile.Create(r.ItemId, plant.Id, item.Sku, item.Name, r.Values);
            repo.Add(profile);
        }
        else
        {
            profile.Update(item.Sku, item.Name, r.Values); // LowLevelCode / StandardCost stay as the system set them (BR-ITM-04)
        }

        await uow.SaveChangesAsync(ct);
        return new ItemProfileDto(profile.ItemId, profile.PlantId, profile.ItemCode, profile.ItemName, false, profile.ToValues(), profile.LowLevelCode, profile.StandardCost);
    }
}

// ── Read ────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>T0.6.3 GetProfileOrDefault: the stored profile, or the defaults of a made item with no lead time.</summary>
public sealed record GetItemProfileQuery(Guid ItemId, Guid PlantId) : IRequest<ItemProfileDto>;

public sealed class GetItemProfileHandler(IPlantAccessService access, IItemProfileRepository repo) : IRequestHandler<GetItemProfileQuery, ItemProfileDto>
{
    public async Task<ItemProfileDto> Handle(GetItemProfileQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var profile = await repo.GetAsync(r.ItemId, plant.Id, ct);
        return profile is null
            ? new ItemProfileDto(r.ItemId, plant.Id, string.Empty, string.Empty, true, ItemManufacturingProfile.DefaultValues, 0, 0m)
            : new ItemProfileDto(profile.ItemId, profile.PlantId, profile.ItemCode, profile.ItemName, false, profile.ToValues(), profile.LowLevelCode, profile.StandardCost);
    }
}

public sealed record ListItemProfilesQuery(Guid PlantId, string? Search, int Page = 1, int PageSize = 50) : IRequest<PagedProfiles>;

public sealed class ListItemProfilesHandler(IPlantAccessService access, IItemProfileRepository repo) : IRequestHandler<ListItemProfilesQuery, PagedProfiles>
{
    public async Task<PagedProfiles> Handle(ListItemProfilesQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var (items, total) = await repo.ListAsync(plant.Id, r.Search, page, size, ct);
        return new PagedProfiles(
            items.Select(p => new ItemProfileListItemDto(p.ItemId, p.ItemCode, p.ItemName, p.MakeOrBuy, p.LotSizingPolicy, p.LowLevelCode)).ToList(),
            total, page, size);
    }
}
