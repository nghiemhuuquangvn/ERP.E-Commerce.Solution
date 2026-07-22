using Asp.Versioning;
using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CRM.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/cases")]
public class CasesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContext;

    public CasesController(IMediator mediator, IUserContextService userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// List cases with optional contact and status filters, plus pagination.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("crm:cases:view")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? contactId,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListCasesQuery(contactId, status, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single case by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("crm:cases:view")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCaseQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Case {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Open a new support case.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("crm:cases:manage")]
    public async Task<IActionResult> Open(
        [FromBody] OpenCaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Assign a case to a user.
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    [AuthorizePermission("crm:cases:manage")]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new AssignCaseCommand(id, request.AssignedToUserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resolve a case with a resolution note.
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [AuthorizePermission("crm:cases:manage")]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolveCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ResolveCaseCommand(id, request.Resolution), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Close a resolved case.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [AuthorizePermission("crm:cases:manage")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CloseCaseCommand(id), cancellationToken);
        return Ok(result);
    }

    public record AssignCaseRequest(Guid AssignedToUserId);
    public record ResolveCaseRequest(string Resolution);
}
