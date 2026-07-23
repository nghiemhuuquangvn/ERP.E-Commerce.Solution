using Asp.Versioning;
using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/masterdata/employee-team-assignments")]
public class EmployeeTeamAssignmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeTeamAssignmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List employee team assignments (paginated) for a given employee.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:employee-team-assignments:view")]
    [ProducesResponseType(typeof(PagedResult<EmployeeTeamAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListEmployeeTeamAssignmentsQuery(employeeId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new employee team assignment.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:employee-team-assignments:manage")]
    [ProducesResponseType(typeof(EmployeeTeamAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeTeamAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get an employee team assignment by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:employee-team-assignments:view")]
    [ProducesResponseType(typeof(EmployeeTeamAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEmployeeTeamAssignmentQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"EmployeeTeamAssignment {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// End an existing employee team assignment.
    /// </summary>
    [HttpPost("{id:guid}/end")]
    [AuthorizePermission("masterdata:employee-team-assignments:manage")]
    [ProducesResponseType(typeof(EmployeeTeamAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> End(
        [FromRoute] Guid id,
        [FromBody] EndEmployeeTeamAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new EndEmployeeTeamAssignmentCommand(id, request.EffectiveTo);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record EndEmployeeTeamAssignmentRequest(DateTime EffectiveTo);
