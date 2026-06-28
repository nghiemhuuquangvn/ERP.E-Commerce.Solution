using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.HRM.API.Controllers;

[ApiController]
[Route("api/v1/hrm/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaveRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List leave requests with optional filtering by employee and status.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("hrm:leave:manage")]
    [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListLeaveRequestsQuery(employeeId, status, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Submit a new leave request.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("hrm:leave:manage")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitLeaveRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a leave request by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("hrm:leave:manage")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLeaveRequestQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Leave request {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Approve a leave request.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [AuthorizePermission("hrm:leave:manage")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Approve(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ApproveLeaveCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reject a leave request.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [AuthorizePermission("hrm:leave:manage")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Reject(
        [FromRoute] Guid id,
        [FromBody] RejectLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new RejectLeaveCommand(id, request.Reason), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cancel a leave request.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [AuthorizePermission("hrm:leave:manage")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CancelLeaveCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record RejectLeaveRequest(string Reason);
