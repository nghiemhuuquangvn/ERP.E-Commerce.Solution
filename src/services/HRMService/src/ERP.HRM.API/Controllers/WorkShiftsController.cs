using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.HRM.API.Controllers;

[ApiController]
[Route("api/v1/hrm/work-shifts")]
public class WorkShiftsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkShiftsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// List all work shifts (optionally filter to active only).
    /// </summary>
    [HttpGet]
    [AuthorizePermission("hrm:shifts:view")]
    [ProducesResponseType(typeof(IEnumerable<WorkShiftDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListWorkShiftsQuery(activeOnly), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single work shift by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("hrm:shifts:view")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetWorkShiftQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Work shift {id} not found." });
        return Ok(result);
    }

    /// <summary>
    /// Create a new work shift.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("hrm:shifts:manage")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkShiftCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update a work shift's name, start time and end time.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("hrm:shifts:manage")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _mediator.Send(
                new UpdateWorkShiftCommand(id, request.Name, request.StartTime, request.EndTime),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deactivate a work shift so it is no longer assignable.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("hrm:shifts:manage")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateWorkShiftCommand(id), cancellationToken);
        return Ok(result);
    }

    public record UpdateShiftRequest(string Name, string StartTime, string EndTime);
}
