using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CRM.API.Controllers;

[ApiController]
[Route("api/v1/crm/activities")]
public class ActivitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ActivitiesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// List activities with optional contact, opportunity and type filters.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("crm:activities:view")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? contactId,
        [FromQuery] Guid? opportunityId,
        [FromQuery] int? activityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListActivitiesQuery(contactId, opportunityId, activityType, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single activity by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("crm:activities:view")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetActivityQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Activity {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Log a new activity (call, email, meeting, note, task) against a contact.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("crm:activities:manage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateActivityCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Mark an activity as completed with an optional completion note.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [AuthorizePermission("crm:activities:manage")]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CompleteActivityCommand(id, request.Notes), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cancel a scheduled activity.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [AuthorizePermission("crm:activities:manage")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CancelActivityCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update the notes on an activity.
    /// </summary>
    [HttpPatch("{id:guid}/notes")]
    [AuthorizePermission("crm:activities:manage")]
    public async Task<IActionResult> UpdateNotes(
        Guid id,
        [FromBody] UpdateNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new UpdateActivityNotesCommand(id, request.Notes), cancellationToken);
        return Ok(result);
    }

    public record CompleteActivityRequest(string? Notes = null);
    public record UpdateNotesRequest(string Notes);
}
