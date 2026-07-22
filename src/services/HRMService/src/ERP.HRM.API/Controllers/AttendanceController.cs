using Asp.Versioning;
using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.HRM.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hrm/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// List attendance records with optional employee, date range and status filters.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("hrm:attendance:view")]
    [ProducesResponseType(typeof(PagedResult<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? employeeId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListAttendanceQuery(employeeId, fromDate, toDate, status, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single attendance record by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("hrm:attendance:view")]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAttendanceQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Attendance record {id} not found." });
        return Ok(result);
    }

    /// <summary>
    /// Get an employee's attendance record for a specific date.
    /// </summary>
    [HttpGet("employee/{employeeId:guid}/date/{date}")]
    [AuthorizePermission("hrm:attendance:view")]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByDate(
        Guid employeeId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAttendanceByDateQuery(employeeId, date), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"No attendance record for employee {employeeId} on {date:yyyy-MM-dd}." });
        return Ok(result);
    }

    /// <summary>
    /// Get a monthly attendance summary for an employee (present/absent/late days, total hours, overtime).
    /// </summary>
    [HttpGet("employee/{employeeId:guid}/summary")]
    [AuthorizePermission("hrm:attendance:view")]
    [ProducesResponseType(typeof(AttendanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        Guid employeeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAttendanceSummaryQuery(employeeId, year, month), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Record a check-in for an employee.
    /// </summary>
    [HttpPost("check-in")]
    [AuthorizePermission("hrm:attendance:manage")]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Record a check-out for an existing attendance record.
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    [AuthorizePermission("hrm:attendance:manage")]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckOut(
        Guid id,
        [FromBody] CheckOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CheckOutCommand(id, request.CheckOutTime, request.Notes), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Mark an employee as absent for a date.
    /// </summary>
    [HttpPost("mark-absent")]
    [AuthorizePermission("hrm:attendance:manage")]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAbsent(
        [FromBody] MarkAbsentCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    public record CheckOutRequest(DateTime CheckOutTime, string? Notes = null);
}
