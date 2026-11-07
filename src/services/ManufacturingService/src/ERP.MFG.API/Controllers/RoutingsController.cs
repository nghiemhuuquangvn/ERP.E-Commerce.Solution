using Asp.Versioning;
using ERP.MFG.Application.Features.Imports;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Routings (BA spec 01 §5.9): versions, operations, approval, lead time and CSV import. Reading needs <c>manufacturing:routing:view</c>, editing a draft
/// <c>manufacturing:routing:manage</c>, approving or making a version obsolete <c>manufacturing:routing:approve</c> (BR-RT-03).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/routings")]
public class RoutingsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, Guid ItemId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Description);
    public sealed record HeaderBody(DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description);
    public sealed record ObsoleteBody(DateOnly? EffectiveTo);
    public sealed record CopyBody(DateOnly? EffectiveFrom);
    public sealed record RenumberBody(List<int> OrderedSeqs);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] Guid? itemId, [FromQuery] VersionStatus? status, CancellationToken ct)
        => Ok(await mediator.Send(new GetRoutingsQuery(plantId, itemId, status), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetRoutingQuery(id), ct));

    /// <summary>The routing that applies to an item on a date (default today); 404 when the item has none (that is allowed, BR-RT-07).</summary>
    [HttpGet("effective")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingView)]
    public async Task<IActionResult> Effective([FromQuery] Guid plantId, [FromQuery] Guid itemId, [FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await mediator.Send(new GetEffectiveRoutingQuery(plantId, itemId, date), ct));

    /// <summary>Standard and calendar minutes per operation and the total for <c>quantity</c>, scheduled backwards from the end of the work of <c>endDate</c> (BR-RT-04, BR-RT-05).</summary>
    [HttpGet("{id:guid}/lead-time")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingView)]
    public async Task<IActionResult> LeadTime(Guid id, [FromQuery] decimal quantity, [FromQuery] DateOnly endDate, CancellationToken ct)
        => Ok(await mediator.Send(new GetRoutingLeadTimeQuery(id, quantity, endDate), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateRoutingCommand(b.PlantId, b.ItemId, b.EffectiveFrom, b.EffectiveTo, b.Description), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] HeaderBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateRoutingHeaderCommand(id, b.EffectiveFrom, b.EffectiveTo, b.Description), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteRoutingCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/operations")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> AddOperation(Guid id, [FromBody] RoutingOperationBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new AddRoutingOperationCommand(id, b), ct));

    /// <summary>Adds an operation with the values of a standard operation (copied, BR-OPS-01); fields in the body override them.</summary>
    [HttpPost("{id:guid}/operations/from-standard/{standardOperationId:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> AddFromStandard(Guid id, Guid standardOperationId, [FromBody] RoutingOperationBody? b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new AddRoutingOperationCommand(id,
            (b ?? new RoutingOperationBody(null, null, null, null, null, null, null, null, null, null, false, null)) with { StandardOperationId = standardOperationId }), ct));

    [HttpPut("{id:guid}/operations/{seq:int}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> UpdateOperation(Guid id, int seq, [FromBody] RoutingOperationBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateRoutingOperationCommand(id, seq, b), ct));

    [HttpDelete("{id:guid}/operations/{seq:int}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> RemoveOperation(Guid id, int seq, CancellationToken ct) => Ok(await mediator.Send(new RemoveRoutingOperationCommand(id, seq), ct));

    /// <summary>The order of the screen after a drag and drop: the operations are renumbered 10, 20, 30 ... in the order given (every current sequence number exactly once).</summary>
    [HttpPost("{id:guid}/renumber")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> Renumber(Guid id, [FromBody] RenumberBody b, CancellationToken ct) => Ok(await mediator.Send(new RenumberRoutingCommand(id, b.OrderedSeqs), ct));

    [HttpPost("{id:guid}/approve")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingApprove)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ApproveRoutingCommand(id), ct));

    [HttpPost("{id:guid}/obsolete")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingApprove)]
    public async Task<IActionResult> Obsolete(Guid id, [FromBody] ObsoleteBody? b, CancellationToken ct) => Ok(await mediator.Send(new ObsoleteRoutingCommand(id, b?.EffectiveTo), ct));

    [HttpPost("{id:guid}/copy")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> Copy(Guid id, [FromBody] CopyBody? b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CopyRoutingCommand(id, b?.EffectiveFrom), ct));

    /// <summary>Imports the operations of a routing from a CSV body (<c>text/csv</c>) as a new Draft version. All-or-nothing, like the BOM import (T2.9).</summary>
    [HttpPost("import")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingManage)]
    public async Task<IActionResult> Import([FromQuery] Guid plantId, [FromQuery] Guid itemId, [FromQuery] DateOnly? effectiveFrom, [FromQuery] bool dryRun, CancellationToken ct)
        => Ok(await mediator.Send(new ImportRoutingCommand(plantId, itemId, effectiveFrom, dryRun, Request.Body), ct));

    [HttpGet("import/template")]
    [AuthorizePermission(PermissionConstants.Manufacturing_RoutingView)]
    public IActionResult ImportTemplate()
        => File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(RoutingCsvTemplate.Csv)).ToArray(), "text/csv", "routing-import-template.csv");
}
