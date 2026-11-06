using Asp.Versioning;
using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Bills of materials (BA spec 01 §5.8): versions, lines, approval, explosion, where-used and comparison. Reading needs <c>manufacturing:bom:view</c>,
/// editing a draft <c>manufacturing:bom:manage</c>, approving or making a version obsolete <c>manufacturing:bom:approve</c> (BR-BOM-02).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/boms")]
public class BomsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, Guid ItemId, decimal? BaseQuantity, decimal? YieldPct, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Description);
    public sealed record HeaderBody(decimal BaseQuantity, decimal YieldPct, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description);
    public sealed record ObsoleteBody(DateOnly? EffectiveTo);
    public sealed record CopyBody(DateOnly? EffectiveFrom);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] Guid? itemId, [FromQuery] VersionStatus? status, CancellationToken ct)
        => Ok(await mediator.Send(new GetBomsQuery(plantId, itemId, status), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetBomQuery(id), ct));

    /// <summary>The version that applies to an item on a date (default today); 400 <c>MFG_PO_004</c> when none.</summary>
    [HttpGet("effective")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> Effective([FromQuery] Guid plantId, [FromQuery] Guid itemId, [FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await mediator.Send(new GetEffectiveBomQuery(plantId, itemId, date), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateBomCommand(b.PlantId, b.ItemId, b.BaseQuantity, b.YieldPct, b.EffectiveFrom, b.EffectiveTo, b.Description), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] HeaderBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateBomHeaderCommand(id, b.BaseQuantity, b.YieldPct, b.EffectiveFrom, b.EffectiveTo, b.Description), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteBomCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/lines")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] BomLineBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new AddBomLineCommand(id, b), ct));

    [HttpPut("{id:guid}/lines/{lineNo:int}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> UpdateLine(Guid id, int lineNo, [FromBody] BomLineBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateBomLineCommand(id, lineNo, b), ct));

    [HttpDelete("{id:guid}/lines/{lineNo:int}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> RemoveLine(Guid id, int lineNo, CancellationToken ct) => Ok(await mediator.Send(new RemoveBomLineCommand(id, lineNo), ct));

    [HttpPost("{id:guid}/approve")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomApprove)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ApproveBomCommand(id), ct));

    [HttpPost("{id:guid}/obsolete")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomApprove)]
    public async Task<IActionResult> Obsolete(Guid id, [FromBody] ObsoleteBody? b, CancellationToken ct)
        => Ok(await mediator.Send(new ObsoleteBomCommand(id, b?.EffectiveTo), ct));

    [HttpPost("{id:guid}/copy")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomManage)]
    public async Task<IActionResult> Copy(Guid id, [FromBody] CopyBody? b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CopyBomCommand(id, b?.EffectiveFrom), ct));

    /// <summary>The materials and outputs of an order for <c>quantity</c> of the item (one level, phantoms passed through). Without <c>bomId</c> the version effective on <c>date</c> is used.</summary>
    [HttpGet("explode")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> Explode([FromQuery] Guid plantId, [FromQuery] Guid itemId, [FromQuery] decimal quantity, [FromQuery] Guid? bomId, [FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await mediator.Send(new ExplodeBomQuery(plantId, itemId, bomId, quantity, date), ct));

    [HttpGet("indented")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> Indented([FromQuery] Guid plantId, [FromQuery] Guid itemId, [FromQuery] decimal quantity, [FromQuery] Guid? bomId, [FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await mediator.Send(new IndentedBomQuery(plantId, itemId, bomId, quantity <= 0 ? 1m : quantity, date), ct));

    [HttpGet("where-used")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> WhereUsed([FromQuery] Guid plantId, [FromQuery] Guid itemId, CancellationToken ct)
        => Ok(await mediator.Send(new WhereUsedQuery(plantId, itemId), ct));

    [HttpGet("compare")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> Compare([FromQuery] Guid left, [FromQuery] Guid right, CancellationToken ct)
        => Ok(await mediator.Send(new CompareBomsQuery(left, right), ct));

    [HttpGet("low-level-codes")]
    [AuthorizePermission(PermissionConstants.Manufacturing_BomView)]
    public async Task<IActionResult> LowLevelCodes([FromQuery] Guid plantId, CancellationToken ct) => Ok(await mediator.Send(new GetLowLevelCodesQuery(plantId), ct));
}
