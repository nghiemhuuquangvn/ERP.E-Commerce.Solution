using Asp.Versioning;
using ERP.MFG.Application.Features.Ncr;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Non-conformance reports (BA spec 01 §5.15 BR-NCR-01..04, BR-RW-01..03). A Failed inspection creates its NCR itself; a person may also raise one from an operation report
/// (<c>manufacturing:qc:inspect</c>, record only). The quality manager splits the quantity into Rework / Scrap / UseAsIs lines (<c>manufacturing:qc:disposition</c>; a UseAsIs line
/// also needs <c>manufacturing:qc:approve-deviation</c>) and executes them one by one. Reading needs <c>manufacturing:qc:view</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/ncrs")]
public class NcrsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid OrderId, Guid ReportId, decimal Quantity, string Description);
    public sealed record DispositionsBody(List<DispositionBody>? Lines);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] NcrStatus? status, [FromQuery] NcrSourceType? source, [FromQuery] Guid? orderId, [FromQuery] string? search,
        [FromQuery] bool openOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetNcrsQuery(plantId, status, source, orderId, search, openOnly, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetNcrQuery(id), ct));

    /// <summary>Raises an NCR by hand from an operation report (it records the finding; nothing moves).</summary>
    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcInspect)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateManualNcrCommand(b.OrderId, b.ReportId, b.Quantity, b.Description), ct));

    /// <summary>Replaces the disposition lines (the sum must be the non-conforming quantity). A UseAsIs line needs the approve-deviation right of the caller.</summary>
    [HttpPut("{id:guid}/dispositions")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcDisposition)]
    public async Task<IActionResult> Dispositions(Guid id, [FromBody] DispositionsBody b, CancellationToken ct)
        => Ok(await mediator.Send(new SaveNcrDispositionsCommand(id, b.Lines ?? []), ct));

    /// <summary>Executes one line: Scrap / UseAsIs move stock in the Warehouse first, Rework creates the rework order.</summary>
    [HttpPost("{id:guid}/dispositions/{lineNo:int}/execute")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcDisposition)]
    public async Task<IActionResult> Execute(Guid id, int lineNo, [FromBody] ExecuteDispositionBody? b, CancellationToken ct)
        => Ok(await mediator.Send(new ExecuteNcrDispositionCommand(id, lineNo, b), ct));
}
