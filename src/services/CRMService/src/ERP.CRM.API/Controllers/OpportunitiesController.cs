using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CRM.API.Controllers;

[ApiController]
[Route("api/v1/crm/opportunities")]
public class OpportunitiesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContext;

    public OpportunitiesController(IMediator mediator, IUserContextService userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// List opportunities with optional contact and stage filters, plus pagination.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("crm:opportunities:view")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? contactId,
        [FromQuery] int? stage,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListOpportunitiesQuery(contactId, stage, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get pipeline summary — counts per stage plus total and won value.
    /// </summary>
    [HttpGet("pipeline/summary")]
    [AuthorizePermission("crm:opportunities:view")]
    public async Task<IActionResult> PipelineSummary(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPipelineSummaryQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single opportunity by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("crm:opportunities:view")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOpportunityQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Opportunity {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Create a new opportunity.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("crm:opportunities:manage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateOpportunityCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Advance an opportunity to the next stage.
    /// </summary>
    [HttpPost("{id:guid}/advance")]
    [AuthorizePermission("crm:opportunities:manage")]
    public async Task<IActionResult> Advance(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new AdvanceOpportunityStageCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Mark an opportunity as won.
    /// </summary>
    [HttpPost("{id:guid}/win")]
    [AuthorizePermission("crm:opportunities:manage")]
    public async Task<IActionResult> Win(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new WinOpportunityCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Mark an opportunity as lost.
    /// </summary>
    [HttpPost("{id:guid}/lose")]
    [AuthorizePermission("crm:opportunities:manage")]
    public async Task<IActionResult> Lose(
        Guid id,
        [FromBody] LoseOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new LoseOpportunityCommand(id, request.Reason), cancellationToken);
        return Ok(result);
    }

    public record LoseOpportunityRequest(string Reason);
}
