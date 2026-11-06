using Asp.Versioning;
using ERP.MFG.Application.Features.ItemProfiles;
using ERP.MFG.Application.Features.Plants;
using ERP.MFG.Application.Features.Settings;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>Plants, their production parameters and who may work in them (BA spec 01 §5.1-5.2, BR-GEN-09).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/plants")]
public class PlantsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetPlantsQuery(activeOnly), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetPlantQuery(id), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreatePlantCommand command, CancellationToken ct = default)
    {
        var plant = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = plant.Id }, plant);
    }

    public sealed record PlantBody(string Name, string? TimeZoneId, Guid RawMaterialWarehouseId, Guid FinishedGoodsWarehouseId,
        Guid QuarantineWarehouseId, Guid? ScrapWarehouseId, Guid? DefaultCalendarId);

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PlantBody b, CancellationToken ct = default)
        => Ok(await mediator.Send(new UpdatePlantCommand(id, b.Name, b.TimeZoneId, b.RawMaterialWarehouseId, b.FinishedGoodsWarehouseId,
            b.QuarantineWarehouseId, b.ScrapWarehouseId, b.DefaultCalendarId), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct = default)
        => Ok(await mediator.Send(new SetPlantActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
        => Ok(await mediator.Send(new SetPlantActiveCommand(id, false), ct));

    // ── Production parameters (A01) ────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/settings")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> GetSettings(Guid id, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetSettingsQuery(id), ct));

    [HttpPut("{id:guid}/settings")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] ProductionSettingValues values, CancellationToken ct = default)
        => Ok(await mediator.Send(new UpdateSettingsCommand(id, values), ct));

    [HttpGet("{id:guid}/settings/audit")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> SettingsAudit(Guid id, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetSettingsAuditQuery(id, take), ct));

    // ── Who may work in the plant (BR-GEN-09) ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/access")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> ListAccess(Guid id, CancellationToken ct = default)
        => Ok(await mediator.Send(new ListPlantAccessQuery(id), ct));

    public sealed record AccessBody(Guid UserId);

    [HttpPost("{id:guid}/access")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> GrantAccess(Guid id, [FromBody] AccessBody b, CancellationToken ct = default)
        => Ok(await mediator.Send(new GrantPlantAccessCommand(id, b.UserId), ct));

    [HttpDelete("{id:guid}/access/{userId:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> RevokeAccess(Guid id, Guid userId, CancellationToken ct = default)
    {
        await mediator.Send(new RevokePlantAccessCommand(id, userId), ct);
        return NoContent();
    }
}

/// <summary>The manufacturing profile of items in a plant (BA spec 01 §5.3).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/item-profiles")]
public class ItemProfilesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await mediator.Send(new ListItemProfilesQuery(plantId, search, page, pageSize), ct));

    /// <summary>The stored profile of an item in a plant, or the documented defaults (isDefault = true) when none was saved.</summary>
    [HttpGet("{itemId:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> Get(Guid itemId, [FromQuery] Guid plantId, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetItemProfileQuery(itemId, plantId), ct));

    /// <summary>Creates or changes the profile. Low-level code and standard cost are not part of the body: the system owns them (BR-ITM-04).</summary>
    [HttpPut("{itemId:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_ItemProfileManage)]
    public async Task<IActionResult> Put(Guid itemId, [FromQuery] Guid plantId, [FromBody] ItemProfileValues values, CancellationToken ct = default)
        => Ok(await mediator.Send(new UpsertItemProfileCommand(itemId, plantId, values), ct));
}
