using Asp.Versioning;
using ERP.MFG.Application.Features.Inspections;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Inspection plans (BA spec 01 §5.15 BR-QC-01): a plan is a series (plant + code) in numbered versions. Reading needs <c>manufacturing:qc:plan-view</c>,
/// editing a draft <c>manufacturing:qc:plan-manage</c>, approving or making a version obsolete <c>manufacturing:qc:plan-approve</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/inspection-plans")]
public class InspectionPlansController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, Guid ItemId, string Code, InspectionType Type, SamplingMethod SamplingMethod, decimal? SampleValue, int AllowedMinorDefects,
        DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Description);
    public sealed record HeaderBody(SamplingMethod SamplingMethod, decimal? SampleValue, int AllowedMinorDefects, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description);
    public sealed record ObsoleteBody(DateOnly? EffectiveTo);
    public sealed record CopyBody(DateOnly? EffectiveFrom);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] InspectionType? type, [FromQuery] Guid? itemId, [FromQuery] VersionStatus? status, CancellationToken ct)
        => Ok(await mediator.Send(new GetInspectionPlansQuery(plantId, type, itemId, status), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetInspectionPlanQuery(id), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateInspectionPlanCommand(b.PlantId, b.ItemId, b.Code, b.Type, b.SamplingMethod, b.SampleValue,
            b.AllowedMinorDefects, b.EffectiveFrom, b.EffectiveTo, b.Description), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] HeaderBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateInspectionPlanHeaderCommand(id, b.SamplingMethod, b.SampleValue, b.AllowedMinorDefects, b.EffectiveFrom, b.EffectiveTo, b.Description), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteInspectionPlanCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/characteristics")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> AddCharacteristic(Guid id, [FromBody] InspectionCharacteristicBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new AddInspectionCharacteristicCommand(id, b), ct));

    [HttpPut("{id:guid}/characteristics/{seq:int}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> UpdateCharacteristic(Guid id, int seq, [FromBody] InspectionCharacteristicBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateInspectionCharacteristicCommand(id, seq, b), ct));

    [HttpDelete("{id:guid}/characteristics/{seq:int}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> RemoveCharacteristic(Guid id, int seq, CancellationToken ct) => Ok(await mediator.Send(new RemoveInspectionCharacteristicCommand(id, seq), ct));

    [HttpPost("{id:guid}/approve")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanApprove)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ApproveInspectionPlanCommand(id), ct));

    [HttpPost("{id:guid}/obsolete")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanApprove)]
    public async Task<IActionResult> Obsolete(Guid id, [FromBody] ObsoleteBody? b, CancellationToken ct) => Ok(await mediator.Send(new ObsoleteInspectionPlanCommand(id, b?.EffectiveTo), ct));

    [HttpPost("{id:guid}/copy")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcPlanManage)]
    public async Task<IActionResult> Copy(Guid id, [FromBody] CopyBody? b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CopyInspectionPlanCommand(id, b?.EffectiveFrom), ct));
}
