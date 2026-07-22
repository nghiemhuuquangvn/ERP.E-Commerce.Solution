using Asp.Versioning;
using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CRM.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/leads")]
public class LeadsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContext;

    public LeadsController(IMediator mediator, IUserContextService userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// List leads with optional status filter and pagination.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("crm:leads:view")]
    public async Task<IActionResult> List(
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListLeadsQuery(status, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single lead by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("crm:leads:view")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLeadQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Lead {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Create a new lead.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("crm:leads:manage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateLeadCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Mark a lead as contacted.
    /// </summary>
    [HttpPost("{id:guid}/contact")]
    [AuthorizePermission("crm:leads:manage")]
    public async Task<IActionResult> Contact(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ContactLeadCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Qualify a lead — creates an opportunity automatically.
    /// </summary>
    [HttpPost("{id:guid}/qualify")]
    [AuthorizePermission("crm:leads:manage")]
    public async Task<IActionResult> Qualify(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new QualifyLeadCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Mark a lead as lost.
    /// </summary>
    [HttpPost("{id:guid}/lose")]
    [AuthorizePermission("crm:leads:manage")]
    public async Task<IActionResult> Lose(
        Guid id,
        [FromBody] LoseLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new LoseLeadCommand(id, request.Reason), cancellationToken);
        return Ok(result);
    }

    public record LoseLeadRequest(string Reason);
}
