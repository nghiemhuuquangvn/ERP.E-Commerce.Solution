using Asp.Versioning;
using ERP.MFG.Application.Features.Inspections;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// IPQC / FQC inspections (BA spec 01 §5.15 BR-QC-02..06). They are created by the system (a Posted receipt line on hold, a Good report at an operation with a plan); here they are read,
/// filled in and submitted (<c>manufacturing:qc:inspect</c>) and a failed one is re-inspected (<c>manufacturing:qc:reinspect</c>). Reading needs <c>manufacturing:qc:view</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/inspections")]
public class InspectionsController(IMediator mediator) : ControllerBase
{
    public sealed record ResultsBody(decimal? SampleQty, List<InspectionResultBody>? Results);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] InspectionType? type, [FromQuery] InspectionStatus? status, [FromQuery] Guid? orderId, [FromQuery] Guid? itemId,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetInspectionsQuery(plantId, type, status, orderId, itemId, search, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetInspectionQuery(id), ct));

    /// <summary>Saves the values typed so far as a draft; the inspection stays Pending.</summary>
    [HttpPut("{id:guid}/results")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcInspect)]
    public async Task<IActionResult> SaveResults(Guid id, [FromBody] ResultsBody b, CancellationToken ct)
        => Ok(await mediator.Send(new SaveInspectionResultsCommand(id, b.SampleQty, b.Results ?? []), ct));

    /// <summary>Evaluates and submits: Passed or Failed, then final. A pass of a final inspection releases the lot in the Warehouse first.</summary>
    [HttpPost("{id:guid}/submit")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcInspect)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] ResultsBody? b, CancellationToken ct)
        => Ok(await mediator.Send(new SubmitInspectionCommand(id, b?.SampleQty, b?.Results), ct));

    [HttpPost("{id:guid}/reinspect")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcReinspect)]
    public async Task<IActionResult> Reinspect(Guid id, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new ReinspectCommand(id), ct));
}
